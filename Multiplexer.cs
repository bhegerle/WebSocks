using System.Net.Sockets;
using System.Net.WebSockets;
using static WebStunnel.Timeouts;

namespace WebStunnel {
    internal class Multiplexer {
        private readonly ChannelConnector channelCon;
        private readonly ISocketMap sockMap;
        private Channel channel;

        internal Multiplexer(ChannelConnector channelCon, ISocketMap sockMap) {
            this.channelCon = channelCon;
            this.sockMap = sockMap;
        }

        internal async Task Multiplex(CancellationToken token) {
            while (true) {
                if (channel != null)
                    await Task.Delay(Config.ReconnectTimeout, token);

                try {
                    channel = await channelCon.Connect(token);
                    if (channel == null)
                        return;
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    await Log.Warn("could not connect channel", e);
                    continue;
                }

                try {
                    using var loopCts = new CancellationTokenSource();
                    using var linkedCts = token.Link(loopCts.Token);

                    var trecv = ChannelReceiveLoop(linkedCts.Token);
                    var srecv = SocketMapLoop(linkedCts.Token);

                    await Task.WhenAny(trecv, srecv);
                    loopCts.Cancel();
                    await Task.WhenAll(trecv, srecv);
                } catch (OperationCanceledException) {
                    throw;
                }

                await sockMap.Reset();
            }
        }

        private async Task ChannelReceiveLoop(CancellationToken token) {
            try {
                var seg = NewSeg();
                var idBuf = new byte[SocketId.Size].AsSegment();

                while (true) {
                    using var recvTimeout = IdleTimeout();
                    using var recvCts = recvTimeout.Token.Link(token);

                    var msg = await channel.Receive(seg, token);

                    var f = new Frame(msg, idBuf.Count, false);
                    var id = new SocketId(f.Suffix);
                    var sock = await sockMap.GetSocket(id);

                    if (f.Message.Count > 0) {
                        using var sendTimeout = SendTimeout();
                        await sock.Send(f.Message, sendTimeout.Token);
                    } else {
                        await sockMap.RemoveSocket(id);
                    }
                }
            } catch (Exception e) {
                await Log.Write("ws receive loop terminated", e);
            }
        }

        private async Task SocketMapLoop(CancellationToken token) {
            try {
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
            } catch (Exception e) {
                await Log.Write("socket map loop terminated", e);
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
}
