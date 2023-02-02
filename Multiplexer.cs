using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebStunnel;

internal class Multiplexer : IDisposable {
    private readonly CancellationTokenSource cts;
    private readonly ServerContext ctx;

    internal Multiplexer(ServerContext ctx) {
        this.ctx = ctx;
        cts = ctx.Link();
        SocketMap = new SocketMap();
    }

    private CancellationToken Token => cts.Token;

    internal SocketMap SocketMap { get; }

    internal async Task Multiplex(WebSocket ws) {
        var wsCtx = new WebSocketContext(ws, ctx, GetSocketTiming());
        await Multiplex(wsCtx);
    }

    internal async Task Multiplex(ClientWebSocket ws) {
        var wsCtx = new WebSocketContext(ws, ctx, GetSocketTiming());
        await Multiplex(wsCtx);
    }

    internal async Task Multiplex(Socket s) {
        var id = new SocketId();
        var sctx = new SocketContext(s, id, null, GetSocketTiming());
        await Multiplex(sctx);
    }

    private async Task<SocketContext> Multiplex(SocketId id) {
        var s = new Socket(SocketType.Stream, ProtocolType.Tcp);
        var sctx = new SocketContext(s, id, ctx.SocketEndPoint, GetSocketTiming());
        await Multiplex(sctx);
        return sctx;
    }

    private async Task Multiplex(SocketContext s) {
        await Log.Write($"multiplexing {s}");
        await SocketMap.Add(s);
    }

    private async Task Multiplex(WebSocketContext wsCtx) {
        var recvAll = SocketMap.Apply(s => SocketReceive(s, wsCtx), Token);

        try {
            await WebSocketReceive(wsCtx);
        } finally {
            await Log.Trace("cancelling socket recv");
            cts.Cancel();
            await recvAll;
            await Log.Trace("exiting ws recv");
        }
    }

    public void Dispose() {
        cts.Dispose();
        SocketMap.Dispose();
    }

    private async Task WebSocketReceive(WebSocketContext wsCtx) {
        var seg = NewSeg(false);

        while (true) {
            ArraySegment<byte> msg;
            SocketId id;

            try {
                (msg, id) = await wsCtx.Receive(seg);
            } catch (Exception e) {
                await Log.Warn("exception while receiving from WebSocket", e);
                break;
            }

            var sock = await SocketMap.TryGet(id);
            try {
                if (sock == null)
                    sock = await Multiplex(id);
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
        var seg = NewSeg(true);

        await Log.Write($"starting socket {sock.Id}");

        while (true) {
            ArraySegment<byte> msg;

            try {
                msg = await sock.Receive(seg);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception e) {
                await Log.Warn("exception while receiving from socket", e);
                msg = seg[..0];
            }

            try {
                await wsCtx.Send(msg, sock.Id);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception e) {
                await Log.Warn("exception while sending to WebSocket", e);
                break;
            }

            if (msg.Count == 0) {
                await Log.Trace($"exiting socket {sock.Id} receive loop");
                break;
            }
        }

        await Log.Trace($"{sock.Id}\tlingering");
        await sock.Linger();

        sock.Dispose();
    }

    private SocketTiming GetSocketTiming() {
        return new SocketTiming(ctx.Config, cts.Token);
    }

    private static ArraySegment<byte> NewSeg(bool allowExtend) {
        var seg = new ArraySegment<byte>(new byte[1024 * 1024]);
        if (allowExtend)
            seg = seg[..Message.Data.SuffixSize];
        return seg;
    }
}
