using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebStunnel;

using static Timeouts;

internal sealed class Multiplexer : IDisposable {
    private readonly ChannelConnector channelCon;
    private readonly ISocketMap sockMap;
    private Channel channel;

    internal Multiplexer(ChannelConnector channelCon, ISocketMap sockMap) {
        this.channelCon = channelCon;
        this.sockMap = sockMap;
    }

    internal static async Task Multiplex(
        WebSocket ws,
        SocketMap2 sockMap,
        Contextualizer ctx) {
        var wsCtx = ctx.Contextualize(ws);
        await Multiplex(wsCtx, sockMap, ctx);
    }

    internal static async Task Multiplex(
        IEnumerable<ClientWebSocket> wsSeq,
        SocketMap2 sockMap,
        Contextualizer ctx) {

        await foreach (var ws in ctx.ApplyRateLimit(wsSeq)) {
            try {
                var wsCtx = ctx.Contextualize(ws);
                await Multiplex(wsCtx, sockMap, ctx);
            } catch {
                // do something
            }
        }
    }

    private static async Task Multiplex(WebSocketContext wsCtx, SocketMap2 sockMap, Contextualizer ctx) {
        using var smCts = ctx.Link();
        var foo = sockMap.ReceiveFromAll((s, t) => SocketReceive(s, sockMap, t), smCts.Token);

        try {
            await WebSocketReceive(wsCtx, sockMap, ctx);
        } finally {
            await foo;
        }
    }

    private static async Task WebSocketReceive(WebSocketContext wsCtx, SocketMap2 sockMap, Contextualizer ctx) {
        var seg = NewSeg();

        while (true) {
            var framedMsg = await wsCtx.Receive(seg);

            var f = new Frame(framedMsg, SocketId.Size, false);
            var id = new SocketId(f.Suffix);
            var msg = f.Message;

            bool rmSock;
            if (msg.Count > 0) {
                SocketContext sock = null;
                try {
                    using var c = ctx.ConnectTimeout();
                    sock = await sockMap.Get(id, c.Token);
                } catch (Exception e) {
                    await Log.Warn($"could not get socket {id}", e);
                }

                try {
                    if (sock != null)
                        await sock.Send(msg);
                    rmSock = false;
                } catch (Exception e) {
                    await Log.Warn($"failed to send to socket {id}", e);
                    rmSock = true;
                }
            } else {
                rmSock = true;
            }

            if (rmSock)
                await sockMap.Remove(id);
        }
    }

    private static Task SocketReceive(SocketContext arg1, SocketMap2 sockMap, CancellationToken token) {
        var seg = NewSeg();
        while (true) {
            var msg = arg1.Receive(seg);

            add id;


        }

        //finally remove from sockmap here
    }

    internal async Task Multiplex(CancellationToken token) {
        while (true) {
            try {
                var c = await Connect(token);
                if (c != null)
                    channel = c;
                else
                    break;

                using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                var trecv = ChannelReceiveLoop(loopCts.Token);
                var srecv = SocketMapLoop(loopCts.Token);

                await Task.WhenAny(trecv, srecv);
                loopCts.Cancel();
                await Task.WhenAll(trecv, srecv);
            } catch (ChannelConnectionException e) {
                await Log.Warn(e.Message, e.InnerException);
            } catch (OperationCanceledException) {
                await Log.Write("multiplexing operation cancelled");
            } catch (Exception e) {
                await Log.Error("other exception while multiplexing", e);
            } finally {
                await sockMap.Reset();
            }

            token.ThrowIfCancellationRequested();
        }
    }

    public void Dispose() {
        channel?.Dispose();
        sockMap.Dispose();
    }

    private async Task<Channel> Connect(CancellationToken token) {
        return await channelCon.Connect(token);
    }

    private async Task ChannelReceiveLoop(CancellationToken token) {
        var seg = NewSeg();
        var idBuf = new byte[SocketId.Size].AsSegment();

        while (true) {
            using var recvTimeout = IdleTimeout(token);
            var msg = await channel.Receive(seg, recvTimeout.Token);

            var f = new Frame(msg, idBuf.Count, false);
            var id = new SocketId(f.Suffix);
            var sock = await sockMap.GetSocket(id, token);

            if (f.Message.Count > 0) {
                using var sendTimeout = SendTimeout(token);
                await sock.Send(f.Message, sendTimeout.Token);
            } else {
                await sockMap.RemoveSocket(id);
            }
        }
    }

    private async Task SocketMapLoop(CancellationToken token) {
        var taskMap = new Dictionary<SocketId, Task>();

        while (true) {
            using var snap = await sockMap.Snapshot();

            try {
                var newInSnap = snap.Sockets.Keys.Except(taskMap.Keys);
                foreach (var sid in newInSnap) {
                    taskMap.Add(sid, SocketReceiveLoop(sid, snap.Sockets[sid]));
                }

                var dropFromSnap = taskMap.Keys.Except(snap.Sockets.Keys);
                foreach (var tid in dropFromSnap) {
                    var t = taskMap[tid];
                    if (await t.DidCompleteWithin(TimeSpan.FromMilliseconds(1)))
                        taskMap.Remove(tid);
                }

                await snap.Lifetime.WhileAlive(token);
            } finally {
                await sockMap.Detach(snap);
            }
        }
    }

    private async Task SocketReceiveLoop(SocketId id, Socket s) {
        var seg = NewSeg();
        var idBuf = id.GetSegment();

        try {
            while (true) {
                using var recvTimeout = IdleTimeout();
                var msg = await s.Receive(seg, recvTimeout.Token);

                var f = new Frame(msg, idBuf.Count, true);
                idBuf.CopyTo(f.Suffix);

                using var sendTimeout = SendTimeout();
                await channel.Send(f.Complete, sendTimeout.Token);

                if (msg.Count == 0)
                    break;
            }
        } finally {
            await sockMap.RemoveSocket(id);
        }
    }

    private static ArraySegment<byte> NewSeg() {
        return new byte[1024 * 1024];
    }
}
