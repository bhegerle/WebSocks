using System.Runtime.CompilerServices;

namespace WebStunnel;

internal sealed class AsyncQueue<T> : IDisposable {
    private readonly Queue<T> queue;
    private readonly SemaphoreSlim mutex, count;

    internal AsyncQueue() {
        queue = new Queue<T>();
        mutex = new SemaphoreSlim(1);
        count = new SemaphoreSlim(0);
    }

    internal async Task Enqueue(T item, CancellationToken token = default) {
        await mutex.WaitAsync(token);
        try {
            queue.Enqueue(item);
        } finally {
            mutex.Release();
        }

        count.Release();
    }

    internal async IAsyncEnumerable<T> Consume([EnumeratorCancellation] CancellationToken token = default) {
        while (true) {
            try {
                await count.WaitAsync(token);
            } catch (OperationCanceledException) {
                yield break;
            }

            try {
                await mutex.WaitAsync(token);
            } catch (OperationCanceledException) {
                yield break;
            }

            try {
                yield return queue.Dequeue();
            } finally {
                mutex.Release();
            }
        }
    }

    public void Dispose() {
        mutex.Dispose();
        count.Dispose();
    }
}
