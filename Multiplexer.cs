﻿using System.Diagnostics.Eventing.Reader;
using System.Net.Sockets;

namespace WebStunnel;

using static Timeouts;

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
