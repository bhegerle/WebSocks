namespace WebStunnel;

internal sealed class SocketMap : IDisposable {
    private readonly SemaphoreSlim mutex;
    private readonly Dictionary<SocketId, SocketContext> map;
    private readonly AsyncQueue<SocketContext> queue;

    internal SocketMap() {
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

    internal async Task Apply(Func<SocketContext, Task> receiver, CancellationToken token) {
        var taskMap = new Dictionary<SocketId, Task>();

        try {
            await foreach (var sock in queue.Consume(token))
                taskMap[sock.Id] = WrapReceiver(sock);
        } catch (OperationCanceledException) {
            if (token.IsCancellationRequested)
                await Task.WhenAll(taskMap.Values);
        }

        async Task WrapReceiver(SocketContext sock) {
            try {
                var rcv = receiver(sock);
                await rcv.WaitAsync(token);
            } catch (Exception e) {
                if (!token.IsCancellationRequested)
                    await Log.Warn("exception receiving from socket", e);
            } finally {
                await Remove(sock);
            }
        }
    }

    public void Dispose() {
        queue.Dispose();
        mutex.Dispose();
    }

    private async Task Remove(SocketContext sock) {
        await mutex.WaitAsync();
        try {
            map.Remove(sock.Id);
            await Log.Trace($"removed socket {sock.Id}");
        } finally {
            mutex.Release();
        }

        sock.Dispose();
    }
}