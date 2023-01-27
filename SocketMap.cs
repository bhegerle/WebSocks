using System.Net.Sockets;

namespace WebStunnel;

internal sealed class SocketMap2 : IDisposable {
    private readonly SemaphoreSlim mutex;
    private readonly IDictionary<SocketId, SocketContext> map;
    private readonly AsyncQueue<SocketMapping> queue;
    private readonly Func<SocketId, CancellationToken, Task<Socket>> resolver;
    private readonly Contextualizer ctx;

    internal SocketMap2(Contextualizer ctx, Func<SocketId, CancellationToken, Task<Socket>> resolver) {
        this.ctx = ctx;
        this.resolver = resolver;
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

    internal async Task<SocketContext> Get(SocketId id, CancellationToken token) {
        using var linked = ctx.Link(token);

        await mutex.WaitAsync(linked.Token);
        try {
            if (map.TryGetValue(id, out var sock))
                return sock;
        } finally {
            mutex.Release();
        }

        var rsock = await resolver(id, token);
        var rctx = ctx.Contextualize(id, rsock);

        await mutex.WaitAsync(linked.Token);
        try {
            map.Add(id, rctx);
            return rctx;
        } finally {
            mutex.Release();
        }
    }

    internal async Task Remove(SocketId id) {
        SocketContext ctx;

        await mutex.WaitAsync();
        try {
            if (!map.TryGetValue(id, out ctx))
                return;
        } finally {
            mutex.Release();
        }

        await queue.Enqueue(new SocketMapping(ctx, false));
    }

    internal async Task Reset() {
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

    internal async Task ReceiveFromAll(Func<SocketContext, CancellationToken, Task> receiver, CancellationToken token) {
        var taskMap = new Dictionary<SocketId, Task>();
        await foreach (var m in queue.Consume(token)) {

        }
    }

    public void Dispose() {
        queue.Dispose();
    }
}

internal sealed record SocketMapping(SocketContext SocketCtx, bool Add);