using System.Net;
using System.Net.Sockets;

namespace WebStunnel;

internal sealed class SocketContext : IDisposable {
    private readonly SemaphoreSlim mutex;
    private readonly Socket sock;
    private readonly IPEndPoint connectTo;
    private readonly SocketTiming sockTime;
    private readonly AsyncQueue<ArraySegment<byte>> sendQueue;
    private bool connected;

    internal SocketContext(Socket sock, SocketId id, IPEndPoint connectTo, SocketTiming sockTime) {
        this.sock = sock;
        this.connectTo = connectTo;
        this.sockTime = sockTime;
        Id = id;

        mutex = new SemaphoreSlim(1);
        sendQueue = new AsyncQueue<ArraySegment<byte>>();

        if (connectTo != null) {
            if (sock.Connected)
                throw new Exception("socket already connected");

            connected = false;
        } else {
            if (!sock.Connected)
                throw new Exception("socket not connected");

            connected = true;
        }
    }

    internal SocketId Id { get; }

    internal async Task Send(ArraySegment<byte> seg) {
        await sendQueue.Enqueue(seg);
    }

    internal async Task SendAndReceive(AsyncQueue<SocketSegment> recvQueue) {
        try {
            if (!connected) {
                using var conTimeout = sockTime.ConnectTimeout();
                await sock.ConnectAsync(connectTo, conTimeout.Token);
                connected = true;
            }
        } catch (Exception e) {
            await Log.Warn($"{Id}\tcon exception", e);
            throw;
        }

        var s = SendLoop();
        var r = RecvLoop(recvQueue);

        try {
            await Task.WhenAll(s, r);
        } finally {
            await Log.Trace($"{Id}\tdone");
        }
    }

    public override string ToString() {
        return $"{Id} connected to {sock.RemoteEndPoint}";
    }

    public void Dispose() {
        sock.Dispose();
        sockTime.Dispose();
        mutex.Dispose();
        sendQueue.Dispose();
    }

    private async Task SendLoop() {
        try {
            await foreach (var seg in sendQueue.Consume(sockTime.Token)) {
                if (seg.Count == 0)
                    break;

                using var sendTimeout = sockTime.SendTimeout();
                await sock.SendAsync(seg, SocketFlags.None, sendTimeout.Token);
                await Log.Trace($"{Id}\tsend {seg.Count}");

                Buffers.Return(seg);
            }

            using var disTimeout = sockTime.ConnectTimeout();
            await sock.DisconnectAsync(false, disTimeout.Token);
            await Log.Trace($"{Id}\tdisconnected, lingering");

            await sockTime.LingerDelay();
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception e) {
            await Log.Warn($"{Id}\tsend exception", e);
            await Cancel();
            throw;
        }
    }

    private async Task RecvLoop(AsyncQueue<SocketSegment> recvQueue) {
        try {
            while (true) {
                var srb = Buffers.New(Id);

                using var recvTimeout = sockTime.IdleTimeout();
                var n = await sock.ReceiveAsync(srb.Seg, SocketFlags.None, recvTimeout.Token);

                srb.Resize(n);
                await Log.Trace($"{Id}\trecv {n}");

                await recvQueue.Enqueue(srb, sockTime.Token);

                if (n == 0)
                    break;
            }
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception e) {
            await Log.Warn($"{Id}\trecv exception", e);
            await Cancel();
            throw;
        }
    }

    private async Task Cancel() {
        await Log.Trace($"{Id}\tcancelled");
        sockTime.Cancel();
    }
}
