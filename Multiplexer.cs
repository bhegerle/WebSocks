using Microsoft.AspNetCore.Mvc;
using System.Net.Sockets;
using System.Net.WebSockets;
using WebStunnel;

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

    internal static async Task Multiplex(WebSocket ws, SocketMap2 sockMap, Contextualizer ctx) {
        var wsCtx = ctx.Contextualize(ws);

        using var ln = ctx.Link();
        var a = sockMap.Apply((s) => SocketReceive(s, wsCtx), ln.Token);

        await WebSocketReceive(wsCtx, sockMap);
    }

    internal static async Task Multiplex(IEnumerable<ClientWebSocket> wsSeq, SocketMap2 sockMap, Contextualizer ctx) {
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

    private static async Task WebSocketReceive(WebSocketContext wsCtx, SocketMap2 sockMap) {
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
                if (msg.Count > 0) {
                    await Dispatch(msg, sock.Id, wsCtx);
                } else {
                    await Log.Write($"closing socket {sock.Id}");
                    break;
                }
            }
        } catch (Exception e) {
            await Log.Warn("exception while receiving from socket", e);
        } finally {
            await sock.Cancel();
        }
    }

    private static async Task Dispatch(ArraySegment<byte> framedMsg, SocketMap2 sockMap) {
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
            await sock?.Cancel();
        }
    }

    private static async Task Dispatch(ArraySegment<byte> msg, SocketId id, WebSocketContext wsCtx) {
        var f = new Frame(msg, SocketId.Size, true);
        id.Write(f.Suffix);

        await wsCtx.Send(f.Complete);
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
        //var seg = NewSeg();
        //var idBuf = id.GetSegment();

        //try {
        //    while (true) {
        //        using var recvTimeout = IdleTimeout();
        //        var msg = await s.Receive(seg, recvTimeout.Token);

        //        var f = new Frame(msg, idBuf.Count, true);
        //        idBuf.CopyTo(f.Suffix);

        //        using var sendTimeout = SendTimeout();
        //        await channel.Send(f.Complete, sendTimeout.Token);

        //        if (msg.Count == 0)
        //            break;
        //    }
        //} finally {
        //    await sockMap.RemoveSocket(id);
        //}
    }

    private static ArraySegment<byte> NewSeg() {
        return new byte[1024 * 1024];
    }
}
