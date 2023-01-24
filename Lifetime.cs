namespace WebStunnel;

internal sealed class Lifetime : IDisposable {
    private readonly SemaphoreSlim sem;

    internal Lifetime() {
        sem = new SemaphoreSlim(0);
    }

    internal void Terminate() {
        sem.Release();
    }

    internal async Task WhileAlive(CancellationToken token) {
        await sem.WaitAsync(token);
    }

    public void Dispose() {
        sem.Dispose();
    }
}
