using System.Formats.Asn1;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;

namespace WebStunnel;

internal class Multiplexer : IDisposable {
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
        var allSockRecv = sockMap.Apply(SocketReceive, Token);

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
                await wsCtx.Send(s.Seg, s.Id);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception e) {
                await Log.Warn("exception while sending to WebSocket", e);
                break;
            }
        }
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

            SocketContext sock;
            try {
                sock = await sockMap.TryGet(id) ?? await Multiplex(id);
            } catch (Exception e) {
                await Log.Warn($"could not get socket {id}", e);
                continue;
            }

            try {
                await sock.Send2(msg);
            } catch (Exception e) {
                await Log.Warn($"could not send to socket {id}", e);
            }
        }
    }

    private async Task SocketReceive(SocketContext sock) {
        await sock.SendAndReceive(wsSendQueue);
    }

    private static async Task SocketReceive(SocketContext sock, WebSocketContext wsCtx) {
        //var seg = NewSeg(true);

        //await Log.Write($"starting socket {sock.Id}");

        //while (true) {
        //    ArraySegment<byte> msg;

        //    try {
        //        msg = await sock.Receive(seg);
        //    } catch (OperationCanceledException) {
        //        break;
        //    } catch (Exception e) {
        //        await Log.Warn("exception while receiving from socket", e);
        //        msg = seg[..0];
        //    }

        //    try {
        //        await wsCtx.Send(msg, sock.Id);
        //    } catch (OperationCanceledException) {
        //        break;
        //    } catch (Exception e) {
        //        await Log.Warn("exception while sending to WebSocket", e);
        //        break;
        //    }

        //    if (msg.Count == 0) {
        //        await Log.Trace($"exiting socket {sock.Id} receive loop");
        //        break;
        //    }
        //}

        //await Log.Trace($"{sock.Id}\tlingering");
        //await sock.Linger();
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
