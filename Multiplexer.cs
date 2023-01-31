using System.Net.WebSockets;

namespace WebStunnel;

internal static class Multiplexer {
    internal static async Task Multiplex(WebSocket ws, SocketMap sockMap, Contextualizer ctx) {
        await Multiplex(sockMap, ctx, ctx.Contextualize(ws));
    }

    internal static async Task Multiplex(IEnumerable<ClientWebSocket> wsSeq, SocketMap sockMap, Contextualizer ctx) {
        await foreach (var ws in ctx.ApplyRateLimit(wsSeq))
            await Multiplex(sockMap, ctx, ctx.Contextualize(ws));
    }

    private static async Task Multiplex(SocketMap sockMap, Contextualizer ctx, WebSocketContext wsCtx) {
        using var ln = ctx.Link();
        var recvAll = sockMap.Apply(s => SocketReceive(s, wsCtx), ln.Token);

        try {
            await WebSocketReceive(wsCtx, sockMap);
        } finally {
            ln.Cancel();
            await recvAll;
        }
    }

    private static async Task WebSocketReceive(WebSocketContext wsCtx, SocketMap sockMap) {
        var seg = NewSeg();

        while (true) {
            ArraySegment<byte> msg;

            try {
                msg = await wsCtx.Receive(seg);
                await Log.Trace($"ws\trecv {msg.Count} (w/suffix)");
            } catch (Exception e) {
                await Log.Warn("exception while receiving from WebSocket", e);
                break;
            }

            var f = new Frame(msg, SocketId.Size, false);
            var id = new SocketId(f.Suffix);

            SocketContext sock;
            try {
                sock = await sockMap.Get(id);
            } catch (Exception e) {
                await Log.Warn($"could not get socket {id}", e);
                continue;
            }

            try {
                await sock.Send(f.Message);
                await Log.Trace($"{id}\tsent {f.Message.Count}");
            } catch (Exception e) {
                await Log.Warn($"could not send to socket {id}", e);
            }
        }
    }

    private static async Task SocketReceive(SocketContext sock, WebSocketContext wsCtx) {
        var seg = NewSeg();

        while (true) {
            ArraySegment<byte> msg;

            try {
                msg = await sock.Receive(seg);
                await Log.Trace($"{sock.Id}\trecv {msg.Count}");
            } catch (Exception e) {
                await Log.Warn("exception while receiving from socket", e);
                msg = seg[..0];
            }

            var f = new Frame(msg, SocketId.Size, true);
            sock.Id.Write(f.Suffix);

            await wsCtx.Send(f.Complete);
            await Log.Trace($"ws\tsent {f.FramedCount} (w/suffix)");

            if (msg.Count == 0) {
                await Log.Write($"exiting socket {sock.Id} receive loop");
                break;
            }
        }

        await Log.Trace($"{sock.Id}\t{sock.Id} lingering");
        await sock.Linger();
    }

    private static ArraySegment<byte> NewSeg() {
        return new byte[1024 * 1024];
    }
}
