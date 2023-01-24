using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;

namespace WebStunnel;

internal record SocketSnapshot(ImmutableDictionary<ulong, Socket> Sockets, Task ReplacementSnapshotAvailable);

internal interface ISocketMap : IDisposable {
    Task<Socket> GetSocket(ulong id);
    Task RemoveSocket(ulong id);
    Task<SocketSnapshot> Snapshot();
}

class SocketMap : ISocketMap {
    private readonly SemaphoreSlim mutex;
    private readonly Dictionary<ulong, Socket> sockMap;
    private SemaphoreSlim repAvailSem;
    private bool tookRepSem;

    internal SocketMap() {
        mutex = new SemaphoreSlim(1);
        sockMap = new Dictionary<ulong, Socket>();
        replSnapTaskSrc = new TaskCompletionSource();
    }

    public async Task<Socket> GetSocket(ulong id) {
        return await GetSocket(id, true);
    }

    public async Task RemoveSocket(ulong id) {
        await mutex.WaitAsync();
        try {
            sockMap.Remove(id);
            ReplaceSnapshot();
        } finally {
            mutex.Release();
        }
    }

    public async Task<SocketSnapshot> Snapshot() {
        await mutex.WaitAsync();
        try {
            return new SocketSnapshot(sockMap.ToImmutableDictionary(), repAvailSem);
        } finally {
            mutex.Release();
        }
    }

    public void Dispose() {
        try {
            mutex.Wait(TimeSpan.FromMilliseconds(1));
        } catch (TimeoutException) {
            Console.WriteLine("could not fully dispose socketMap due to mutex exception");
            return;
        }

        foreach (var s in sockMap.Values)
            s.Dispose();

        mutex.Release();
    }

    internal async Task<Socket> GetSocket(ulong id, bool required) {
        await mutex.WaitAsync();
        try {
            if (sockMap.TryGetValue(id, out var s))
                return s;
            else if (required)
                throw new Exception($"no socket found with id {id}");
            else
                return null;
        } finally {
            mutex.Release();
        }
    }

    internal async Task AddSocket(ulong id, Socket s) {
        if (id == 0)
            throw new Exception("nonzero id required");

        await mutex.WaitAsync();
        try {
            sockMap.Add(id, s);
            ReplaceSnapshot();
        } finally {
            mutex.Release();
        }
    }

    private void ReplaceSnapshot() {
        replSnapTaskSrc.SetResult();
        replSnapTaskSrc=new TaskCompletionSource();
    }
}

class AutoconnectSocketMap : ISocketMap {
    private readonly IPEndPoint endPoint;
    private readonly SocketMap sockMap;

    internal AutoconnectSocketMap(IPEndPoint endPoint) {
        this.endPoint = endPoint;
        sockMap = new SocketMap();
    }

    public async Task<Socket> GetSocket(ulong id) {
        await Task.WhenAny();
        var s = await sockMap.GetSocket(id, false);
        if (s == null) {
            throw new Exception("not impl");
        }
        return s;
    }

    public async Task RemoveSocket(ulong id) {
        await sockMap.RemoveSocket(id);
    }

    public async Task<SocketSnapshot> Snapshot() {
        return await sockMap.Snapshot();
    }

    public void Dispose() {
        sockMap.Dispose();
    }
}
