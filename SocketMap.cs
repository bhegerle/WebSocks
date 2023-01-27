using System.Collections.Generic;
using System.Net.Sockets;

namespace WebStunnel;

internal sealed class SocketMap : IDisposable {
    private readonly Contextualizer ctx;
    private readonly Func<Socket> socketCon;
    private readonly SemaphoreSlim mutex;
    private readonly IDictionary<SocketId, SocketContext> map;
    private readonly AsyncQueue<SocketContext> queue;

    internal SocketMap(Contextualizer ctx, Func<Socket> socketCon) {
        this.ctx = ctx;
        this.socketCon = socketCon;
        mutex = new SemaphoreSlim(1);
        map = new Dictionary<SocketId, SocketContext>();
        queue = new AsyncQueue<SocketContext>();
    }

    internal async Task Add(SocketContext sock) {
        await mutex.WaitAsync();
        try {
            map.Add(sock.Id, sock);
        } finally {
            mutex.Release();
        }

        await queue.Enqueue(sock);
    }

    internal async Task<SocketContext> TryGet(SocketId id) {
        await mutex.WaitAsync();
        try {
            if (map.TryGetValue(id, out var sock))
                return sock;
            else
                return null;
        } finally {
            mutex.Release();
        }
    }

    internal async Task<SocketContext> Get(SocketId id) {
        await mutex.WaitAsync();
        try {
            if (map.TryGetValue(id, out var sock))
                return sock;
        } finally {
            mutex.Release();
        }

        var newSock = ctx.Contextualize(id, socketCon());

        await mutex.WaitAsync();
        try {
            map.Add(id, newSock);
        } finally {
            mutex.Release();
        }

        await queue.Enqueue(newSock);

        return newSock;
    }

    internal async Task CancelAll() {
        List<SocketContext> list;

        await mutex.WaitAsync();
        try {
            list = map.Values.ToList();
        } finally {
            mutex.Release();
        }

        foreach (var sc in list)
            try {
                await sc.Cancel();
            } catch (OperationCanceledException) {
                // ignored
            }
    }

    internal async Task Apply(Func<SocketContext, Task> receiver, CancellationToken token) {
        var taskMap = new Dictionary<SocketId, Task>();

        try {
            await foreach (var sock in queue.Consume(token)) {
                taskMap[sock.Id] = X(sock);
            }
        } catch (OperationCanceledException) {
            if (token.IsCancellationRequested)
                await Task.WhenAll(taskMap.Values);
        }

        async Task X(SocketContext sock) {
            try {
                var rcv = receiver(sock);
                await rcv.WaitAsync(token);
            } catch (Exception e) {
                if (!token.IsCancellationRequested)
                    await Log.Warn($"exception receiving from socket", e);
            } finally {
                await Remove(sock);
            }
        }
    }

    public void Dispose() {
        queue.Dispose();
    }

    private async Task Remove(SocketContext sock) {
        await mutex.WaitAsync();
        try {
            map.Remove(sock.Id);
        } finally {
            mutex.Release();
        }
    }

    private sealed record SocketMapping(SocketContext SocketCtx, bool Add) {
        public override string ToString() {
            var a = Add ? "add" : "remove";
            return $"{a} socket mapping for {SocketCtx.Id}";
        }
    }
}