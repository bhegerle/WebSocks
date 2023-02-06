using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebStunnel;

internal sealed class Multiplexer : IDisposable {
    private readonly CancellationTokenSource cts;
    private readonly ServerContext ctx;
    private readonly SocketMap sockMap;
    private readonly AsyncQueue<SocketSegment> wsSendQueue;

    internal Multiplexer(ServerContext ctx) {
        this.ctx = ctx;
        cts = ctx.Link();
        sockMap = new SocketMap();
        wsSendQueue = new AsyncQueue<SocketSegment>();
    }

    private CancellationToken Token => cts.Token;

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
        await sockMap.Add(s);
    }

    private async Task Multiplex(WebSocketContext wsCtx) {
        var wsSend = WebSocketSend(wsCtx);
        var allSockRecv = sockMap.Apply(s => s.SendAndReceive(wsSendQueue), Token);

        try {
            await WebSocketReceive(wsCtx);
        } finally {
            await Log.Trace("cancelling socket recv");
            cts.Cancel();
            await Task.WhenAll(wsSend, allSockRecv);
            await Log.Trace("exiting ws recv");
        }
    }

    public void Dispose() {
        cts.Dispose();
        sockMap.Dispose();
        wsSendQueue.Dispose();
    }

    private async Task WebSocketSend(WebSocketContext wsCtx) {
        await foreach (var s in wsSendQueue.Consume(Token)) {
            try {
                await wsCtx.Send(s);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception e) {
                await Log.Warn("exception while sending to WebSocket", e);
                break;
            }
        }
    }

    private async Task WebSocketReceive(WebSocketContext wsCtx) {
        while (true) {
            SocketSegment seg;
            try {
                seg = await wsCtx.Receive();
            } catch (Exception e) {
                await Log.Warn("exception while receiving from WebSocket", e);
                break;
            }

            SocketContext sock;
            try {
                sock = await sockMap.TryGet(seg.Id) ?? await Multiplex(seg.Id);
            } catch (Exception e) {
                await Log.Warn($"could not get socket {seg.Id}", e);
                continue;
            }

            try {
                await sock.Send(seg.Seg);
            } catch (Exception e) {
                await Log.Warn($"could not send to socket {seg.Id}", e);
            }
        }
    }

    private SocketTiming GetSocketTiming() {
        return new SocketTiming(ctx.Config, cts.Token);
    }
}
