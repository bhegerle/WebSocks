using System.Net.WebSockets;

namespace WebStunnel;

internal static class Multiplexer {
    internal static async Task Multiplex(WebSocket ws, SocketMap sockMap, Contextualizer ctx) {
        var wsCtx = ctx.Contextualize(ws);

        using var ln = ctx.Link();
        var recvAll = sockMap.Apply((s) => SocketReceive(s, wsCtx), ln.Token);

        try {
            await WebSocketReceive(wsCtx, sockMap);
        } finally {
            ln.Cancel();
            await recvAll;
        }
    }

    internal static async Task Multiplex(IEnumerable<ClientWebSocket> wsSeq, SocketMap sockMap, Contextualizer ctx) {
        await foreach (var ws in ctx.ApplyRateLimit(wsSeq))
            await Multiplex(ws, sockMap, ctx);
    }

    private static async Task WebSocketReceive(WebSocketContext wsCtx, SocketMap sockMap) {
        var seg = NewSeg();

        while (true) {
            ArraySegment<byte> framedMsg;

            try {
                framedMsg = await wsCtx.Receive(seg);
            } catch (Exception e) {
                await Log.Warn("exception while receiving from WebSocket", e);
                break;
            }

            await Log.Trace($"received {framedMsg.Count} byte message (framed) from WebSocket");

            await SocketDispatch(framedMsg, sockMap);
        }
    }

    private static async Task SocketReceive(SocketContext sock, WebSocketContext wsCtx) {
        var seg = NewSeg();

        while (true) {
            ArraySegment<byte> msg;

            try {
                msg = await sock.Receive(seg);
                await Log.Trace($"received {msg.Count} byte message from socket {sock.Id}");
            } catch (Exception e) {
                await Log.Warn("exception while receiving from socket", e);
                msg = seg[..0];
            }

            await WebSocketDispatch(msg, sock.Id, wsCtx);

            if (msg.Count == 0) {
                await Log.Write($"exiting socket {sock.Id} receive loop");
                break;
            }
        }
    }

    private static async Task SocketDispatch(ArraySegment<byte> framedMsg, SocketMap sockMap) {
        var f = new Frame(framedMsg, SocketId.Size, false);
        var id = new SocketId(f.Suffix);
        var msg = f.Message;

        if (msg.Count > 0) {
            SocketContext sock;
            try {
                sock = await sockMap.Get(id);
            } catch (Exception e) {
                await Log.Warn($"could not get socket {id}", e);
                return;
            }

            try {
                await Log.Trace($"dispatching {msg.Count} byte message to socket {id}");
                await sock.Send(msg);
            } catch (Exception e) {
                await Log.Warn($"could not send to socket {id}", e);
            }
        } else {
            var sock = await sockMap.TryGet(id);
            if (sock != null)
                await sock.Cancel();
        }
    }

    private static async Task WebSocketDispatch(ArraySegment<byte> msg, SocketId id, WebSocketContext wsCtx) {
        var f = new Frame(msg, SocketId.Size, true);
        id.Write(f.Suffix);

        await Log.Trace($"dispatching {f.FramedCount} byte message (framed) from socket {id} to WebSocket");

        await wsCtx.Send(f.Complete);
    }

    private static ArraySegment<byte> NewSeg() {
        return new byte[1024 * 1024];
    }
}
