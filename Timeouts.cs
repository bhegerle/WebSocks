namespace WebStunnel;

internal static class Timeouts {
    internal static CancellationTokenSource SendTimeout() {
        return Timeout(Config.Timeout);
    }

    internal static CancellationTokenSource ConnectTimeout() {
        return Timeout(Config.ConnectTimeout);
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
