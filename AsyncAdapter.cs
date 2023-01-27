namespace WebStunnel;

internal class AsyncAdapter<T> : IAsyncEnumerable<T> {
    private readonly IEnumerable<T> seq;

    internal AsyncAdapter(IEnumerable<T> seq) {
        this.seq = seq;
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken _ = default) {
        return new Enumerator(seq.GetEnumerator());
    }

    internal class Enumerator : IAsyncEnumerator<T> {
        private readonly IEnumerator<T> e;

        internal Enumerator(IEnumerator<T> enumerator) {
            e = enumerator;
        }

        public T Current => e.Current;

        public ValueTask DisposeAsync() {
            e.Dispose();
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> MoveNextAsync() {
            return ValueTask.FromResult(e.MoveNext());
        }
    }
}

internal static class AsyncAdapter {
    internal static IAsyncEnumerable<T> AsAsync<T>(this IEnumerable<T> seq) {
        return new AsyncAdapter<T>(seq);
    }
}
