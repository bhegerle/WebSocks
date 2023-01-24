using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;

namespace WebStunnel;

internal record SocketSnapshot(ImmutableDictionary<ulong, Socket> Sockets, Lifetime Lifetime) : IDisposable {
    public void Dispose() {
        Lifetime.Dispose();
    }
}

internal interface ISocketMap {
    Task<Socket> GetSocket(ulong id);
    Task RemoveSocket(ulong id);
    Task Reset();
    Task<SocketSnapshot> Snapshot();
}

class SocketMap : ISocketMap {
    private readonly SemaphoreSlim mutex;
    private readonly Dictionary<ulong, Socket> sockMap;
    private Lifetime lastSnapLifetime; // owned by that Snapshot

    internal SocketMap() {
        mutex = new SemaphoreSlim(1);
        sockMap = new Dictionary<ulong, Socket>();
    }

    public async Task<Socket> GetSocket(ulong id) {
        return await GetSocket(id, true);
    }

    public async Task RemoveSocket(ulong id) {
        await mutex.WaitAsync();
        try {
            if (sockMap.TryGetValue(id, out var s)) {
                s.Dispose();
                sockMap.Remove(id);
            }

            ReplaceSnapshot();
        } finally {
            mutex.Release();
        }

        Console.WriteLine($"removed connection {id}");
    }

    public async Task Reset() {
        await mutex.WaitAsync();
        try {
            foreach (var s in sockMap.Values)
                s.Dispose();

            lastSnapLifetime = null;
        } finally {
            mutex.Release();
        }

        Console.WriteLine($"reset all connections");
    }

    public async Task<SocketSnapshot> Snapshot() {
        await mutex.WaitAsync();
        try {
            if (lastSnapLifetime != null)
                throw new Exception("concurrent snapshots not supported");

            lastSnapLifetime = new Lifetime();

            return new SocketSnapshot(sockMap.ToImmutableDictionary(), lastSnapLifetime);
        } finally {
            mutex.Release();
        }
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

        Console.WriteLine($"added connection {id}");
    }

    public void Dispose() {
        foreach (var s in sockMap.Values)
            s.Dispose();

        mutex.Dispose();
    }

    private void ReplaceSnapshot() {
        if (lastSnapLifetime != null)
            lastSnapLifetime.Terminate();

        lastSnapLifetime = null;
    }
}

class AutoconnectSocketMap : ISocketMap {
    private readonly IPEndPoint endPoint;
    private readonly SemaphoreSlim mutex;
    private readonly SocketMap sockMap;

    internal AutoconnectSocketMap(IPEndPoint endPoint) {
        this.endPoint = endPoint;
        mutex = new SemaphoreSlim(1);
        sockMap = new SocketMap();
    }

    public async Task<Socket> GetSocket(ulong id) {
        await mutex.WaitAsync();
        try {
            var s = await sockMap.GetSocket(id, false);
            if (s == null) {
                s = await Connect();
                await sockMap.AddSocket(id, s);
            }

            return s;
        } finally {
            mutex.Release();
        }
    }

    public async Task RemoveSocket(ulong id) {
        await mutex.WaitAsync();
        try {
            await sockMap.RemoveSocket(id);
        } finally {
            mutex.Release();
        }
    }

    public async Task Reset() {
        await mutex.WaitAsync();
        try {
            await sockMap.Reset();
        } finally {
            mutex.Release();
        }
    }

    public async Task<SocketSnapshot> Snapshot() {
        await mutex.WaitAsync();
        try {
            return await sockMap.Snapshot();
        } finally {
            mutex.Release();
        }
    }

    public void Dispose() {
        sockMap.Dispose();
        mutex.Dispose();
    }

    private async Task<Socket> Connect() {
        var s = new Socket(SocketType.Stream, ProtocolType.Tcp);

        using var conTimeout = Timeouts.ConnectTimeout();
        await s.ConnectAsync(endPoint, conTimeout.Token);

        Console.WriteLine($"connected new socket to {endPoint}");

        return s;
    }
}
