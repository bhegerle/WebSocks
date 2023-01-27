using System.Net.WebSockets;

namespace WebStunnel;

internal static class Multiplexer {
    internal static async Task Multiplex(WebSocket ws, SocketMap sockMap, Contextualizer ctx) {
        var wsCtx = ctx.Contextualize(ws);

        using var ln = ctx.Link();
        var a = sockMap.Apply((s) => SocketReceive(s, wsCtx), ln.Token);

        await WebSocketReceive(wsCtx, sockMap);
    }

    internal static async Task Multiplex(IEnumerable<ClientWebSocket> wsSeq, SocketMap sockMap, Contextualizer ctx) {
        await foreach (var ws in ctx.ApplyRateLimit(wsSeq)) {
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
    }

    private static async Task WebSocketReceive(WebSocketContext wsCtx, SocketMap sockMap) {
        var seg = NewSeg();

        try {
            while (true) {
                var framedMsg = await wsCtx.Receive(seg);
                await Dispatch(framedMsg, sockMap);
            }
        } catch (Exception e) {
            await Log.Warn("exception while receiving from WebSocket", e);
        } finally {
            await sockMap.CancelAll();
        }
    }

    private static async Task SocketReceive(SocketContext sock, WebSocketContext wsCtx) {
        var seg = NewSeg();

        try {
            while (true) {
                var msg = await sock.Receive(seg);
                await Dispatch(msg, sock.Id, wsCtx);

                if (msg.Count == 0) {
                    await Log.Write($"closing socket {sock.Id}");
                    break;
                }
            }
        } catch (Exception e) {
            await Log.Warn("exception while receiving from socket", e);
        }
    }

    private static async Task Dispatch(ArraySegment<byte> framedMsg, SocketMap sockMap) {
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
                await sock.Send(f.Message);
            } catch (Exception e) {
                await Log.Warn($"could not send to socket {id}", e);
            }
        } else {
            var sock = await sockMap.TryGet(id);
            if (sock != null)
                await sock.Cancel();
        }
    }

    private static async Task Dispatch(ArraySegment<byte> msg, SocketId id, WebSocketContext wsCtx) {
        var f = new Frame(msg, SocketId.Size, true);
        id.Write(f.Suffix);

        await wsCtx.Send(f.Complete);
    }

    private static ArraySegment<byte> NewSeg() {
        return new byte[1024 * 1024];
    }
}
