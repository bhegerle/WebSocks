namespace WebStunnel;

internal static class Repeatedly {
    internal static IEnumerable<T> Invoke<T>(Func<T> x) {
        while (true)
            yield return x();
    }
}
