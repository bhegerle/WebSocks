using System.Net;
using System.Net.Sockets;

namespace WebStunnel;

using static Timeouts;

internal sealed class SocketContext : IDisposable {
    private readonly SemaphoreSlim mutex;
    private readonly Socket sock;
    private readonly CancellationTokenSource cts;
    private readonly IPEndPoint connectTo;
    private bool connected;

    internal SocketContext(Socket sock, SocketId id, IPEndPoint connectTo, Contextualizer ctx) {
        this.sock = sock;
        Id = id;
        this.connectTo = connectTo;
        cts = ctx.Link();

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
        using var sendTimeout = SendTimeout(cts.Token);
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
        using var recvTimeout = IdleTimeout(cts.Token);
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
        cts.Cancel();
    }

    public void Dispose() {
        sock.Dispose();
        cts.Dispose();
        mutex.Dispose();
    }

    private async Task Check(CancellationToken token) {
        await mutex.WaitAsync(token);
        try {
            if (!connected) {
                using var conTimeout = ConnectTimeout(token);
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
