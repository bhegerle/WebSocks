using System.Runtime.CompilerServices;

namespace WebStunnel;

internal static class RateLimiting {
    internal static async IAsyncEnumerable<T> RateLimited<T>(this IEnumerable<T> seq, TimeSpan delay,
        [EnumeratorCancellation] CancellationToken token = default) {
        var first = true;

        foreach (var ws in seq) {
            if (first)
                first = false;
            else
                try {
                    await Task.Delay(delay, token);
                } catch (OperationCanceledException) {
                    yield break;
                }

            yield return ws;
        }
    }
}
