using System.Net.WebSockets;

namespace WebStunnel;

internal static class Multiplexer {
    // problem that WebSocket contextualized here, and not lower in loop
    internal static async Task Multiplex(WebSocket ws, Contextualizer ctx) {
        using var sockMap = new SocketMap();
        await Multiplex(sockMap, ctx, subCtx => subCtx.Contextualize(ws));
    }

    internal static async Task Multiplex(IEnumerable<ClientWebSocket> wsSeq, SocketMap sockMap, Contextualizer ctx) {
        try {
            await foreach (var ws in ctx.ApplyRateLimit(wsSeq)) {
                await Multiplex(sockMap, ctx, subCtx => subCtx.Contextualize(ws));
            }
        } finally {
            await Log.Trace("exiting multiplex loop");
        }
    }

    private static async Task Multiplex(SocketMap sockMap, Contextualizer ctx, Func<Contextualizer, WebSocketContext> cons) {
        using var c = new CancellationTokenSource();
        using var subCtx = ctx.Subcontext(c.Token);

        using var wsCtx = cons(subCtx);

        var recvAll = sockMap.Apply(s => SocketReceive(s, wsCtx), subCtx.Token);

        try {
            await WebSocketReceive(wsCtx, sockMap, subCtx);
        } finally {
            c.Cancel();
            await recvAll;
            await Log.Trace("exiting ws recv");
        }
    }

    private static async Task WebSocketReceive(WebSocketContext wsCtx, SocketMap sockMap, Contextualizer ctx) {
        var seg = NewSeg();

        while (true) {
            ArraySegment<byte> msg;
            SocketId id;

            try {
                (msg, id) = await wsCtx.Receive(seg);
            } catch (Exception e) {
                await Log.Warn("exception while receiving from WebSocket", e);
                break;
            }

            var sock = await sockMap.TryGet(id);
            try {
                if (sock == null) {
                    sock = ctx.Contextualize(id);
                    await sockMap.Add(sock);
                }
            } catch (Exception e) {
                await Log.Warn($"could not get socket {id}", e);
                continue;
            }

            try {
                await sock.Send(msg);
            } catch (Exception e) {
                await Log.Warn($"could not send to socket {id}", e);
            }
        }
    }

    private static async Task SocketReceive(SocketContext sock, WebSocketContext wsCtx) {
        var seg = NewSeg();

        await Log.Write($"starting socket {sock.Id}");

        while (true) {
            ArraySegment<byte> msg;

            try {
                msg = await sock.Receive(seg);
            } catch (Exception e) {
                await Log.Warn("exception while receiving from socket", e);
                msg = seg[..0];
            }

            await wsCtx.Send(msg, sock.Id);

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
