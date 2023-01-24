namespace WebStunnel;

internal static class Timeouts {
    internal static CancellationTokenSource Timeout() {
        return Timeout(Config.Timeout);
    }

    internal static CancellationTokenSource IdleTimeout() {
        return Timeout(Config.IdleTimeout);
    }

    internal static CancellationTokenSource Timeout(TimeSpan timeout) {
        var c = new CancellationTokenSource();
        c.CancelAfter(timeout);
        return c;
    }
}
