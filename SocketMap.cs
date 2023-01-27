using System.Net.Sockets;

namespace WebStunnel;

internal sealed class SocketMap : IDisposable {
    private readonly Contextualizer ctx;
    private readonly Func<Socket> socketCon;
    private readonly SemaphoreSlim mutex;
    private readonly IDictionary<SocketId, SocketContext> map;
    private readonly AsyncQueue<SocketMapping> queue;

    internal SocketMap(Contextualizer ctx, Func<Socket> socketCon) {
        this.ctx = ctx;
        this.socketCon = socketCon;
        mutex = new SemaphoreSlim(1);
        map = new Dictionary<SocketId, SocketContext>();
        queue = new AsyncQueue<SocketMapping>();
    }

    internal async Task Add(SocketContext sockCtx) {
        await mutex.WaitAsync();
        try {
            map.Add(sockCtx.Id, sockCtx);
        } finally {
            mutex.Release();
        }

        await queue.Enqueue(new SocketMapping(sockCtx, true));
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
            if (!map.TryGetValue(id, out var sock)) {
                sock = ctx.Contextualize(id, socketCon());
                map.Add(id, sock);
                await queue.Enqueue(new SocketMapping(sock, true));
            }

            return sock;
        } finally {
            mutex.Release();
        }
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
            await foreach (var m in queue.Consume(token)) {
                try {
                    var id = m.SocketCtx.Id;
                    if (m.Add) {
                        taskMap[id] = receiver(m.SocketCtx);
                    } else {
                        if(taskMap.TryGetValue(id, out var t)){
                            await t.WaitAsync(token);
                        }   }
                } catch (Exception e) {
                    await Log.Warn($"excepting {m}", e);
                }
            }
        } finally {
            await Task.WhenAll(taskMap.Values);
        }
    }

    public void Dispose() {
        queue.Dispose();
    }

    private sealed record SocketMapping(SocketContext SocketCtx, bool Add) {
        public override string ToString() {
            var a = Add ? "add" : "remove";
            return $"{a} socket mapping for {SocketCtx.Id}";
        }
    }
}