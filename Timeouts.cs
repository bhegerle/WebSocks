namespace WebStunnel;

internal static class Timeouts {
    internal static CancellationTokenSource SendTimeout() {
        return Timeout(Config.SendTimeout);
    }

    internal static CancellationTokenSource ConnectTimeout() {
        return Timeout(Config.ConnectTimeout);
    }

    internal static CancellationTokenSource IdleTimeout() {
        return Timeout(Config.IdleTimeout);
    }

    internal static CancellationTokenSource SendTimeout(CancellationToken token) {
        return Timeout(token, Config.SendTimeout);
    }

    internal static CancellationTokenSource ConnectTimeout(CancellationToken token) {
        return Timeout(token, Config.ConnectTimeout);
    }

    internal static CancellationTokenSource IdleTimeout(CancellationToken token) {
        return Timeout(token, Config.IdleTimeout);
    }

    private static CancellationTokenSource Timeout(TimeSpan timeout) {
        var c = new CancellationTokenSource();
        c.CancelAfter(timeout);
        return c;
    }

    private static CancellationTokenSource Timeout(CancellationToken token, TimeSpan timeout) {
        var c = CancellationTokenSource.CreateLinkedTokenSource(token);
        c.CancelAfter(timeout);
        return c;
    }

}
