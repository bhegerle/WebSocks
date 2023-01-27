using System.Net;
using System.Net.Sockets;

namespace WebStunnel;

internal sealed class SocketContext : IDisposable {
    private readonly SemaphoreSlim mutex;
    private readonly Socket sock;
    private readonly IPEndPoint connectTo;
    private readonly SocketCancellation cancellation;
    private bool connected;

    internal SocketContext(Socket sock, SocketId id, IPEndPoint connectTo, SocketCancellation cancellation) {
        this.sock = sock;
        this.connectTo = connectTo;
        this.cancellation = cancellation;
        
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
        using var sendTimeout = cancellation.SendTimeout();
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
        using var recvTimeout = cancellation.IdleTimeout();
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

    internal async Task Cancel() {
        await Log.Warn($"socket {Id} cancelled");
        cancellation.Cancel();
    }

    public void Dispose() {
        sock.Dispose();
        cancellation.Dispose();
        mutex.Dispose();
    }

    private async Task Check(CancellationToken token) {
        await mutex.WaitAsync(token);
        try {
            if (!connected) {
                using var conTimeout = cancellation.ConnectTimeout(token);
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
