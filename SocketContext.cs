using System.Net;
using System.Net.Sockets;

namespace WebStunnel;

internal sealed class SocketContext : IDisposable {
    private readonly SemaphoreSlim mutex;
    private readonly Socket sock;
    private readonly IPEndPoint connectTo;
    private readonly SocketTiming sockTime;
    private bool connected, readZero;

    internal SocketContext(Socket sock, SocketId id, IPEndPoint connectTo, SocketTiming sockTime) {
        this.sock = sock;
        this.connectTo = connectTo;
        this.sockTime = sockTime;

        Id = id;

        mutex = new SemaphoreSlim(1);

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
        using var sendTimeout = sockTime.SendTimeout();
        await Check(sendTimeout.Token);

        try {
            await sock.SendAsync(seg, SocketFlags.None, sendTimeout.Token);
        } catch (Exception e) {
            await Log.Warn("socket send exception", e);
            await Cancel();
            throw;
        }
    }

    internal async Task<ArraySegment<byte>> Receive(ArraySegment<byte> seg) {
        using var recvTimeout = sockTime.IdleTimeout();
        await Check(recvTimeout.Token);

        try {
            var n = await sock.ReceiveAsync(seg, SocketFlags.None, recvTimeout.Token);
            return seg[..n];
        } catch (Exception e) {
            await Log.Warn("socket receive exception", e);
            await Cancel();
            throw;
        }
    }

    internal async Task Linger() {
        await sockTime.LingerDelay();
    }

    internal async Task Cancel() {
        await Log.Warn($"socket {Id} cancelled");
        sockTime.Cancel();
    }

    public void Dispose() {
        sock.Dispose();
        sockTime.Dispose();
        mutex.Dispose();
    }

    private async Task Check(CancellationToken token) {
        await mutex.WaitAsync(token);
        try {
            if (!connected) {
                using var conTimeout = sockTime.ConnectTimeout(token);
                await sock.ConnectAsync(connectTo, conTimeout.Token);
                connected = true;
            }
        } catch (Exception e) {
            await Log.Warn("socket connect exception", e);
            await Cancel();
            throw;
        } finally {
            mutex.Release();
        }
    }
}
