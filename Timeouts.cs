namespace WebStunnel;

//internal   class Timeouts {
//    private static TimeSpan sendTimeout, connectTimeout, idleTimeout;

//    internal static void Configure(Config config) {
//        sendTimeout = config.SendTimeout;
//        connectTimeout = config.ConnectTimeout;
//        idleTimeout = config.IdleTimeout;
//    }

//    internal static CancellationTokenSource SendTimeout() {
//        return Timeout(sendTimeout);
//    }

//    internal static CancellationTokenSource ConnectTimeout() {
//        return Timeout(connectTimeout);
//    }

//    internal static CancellationTokenSource IdleTimeout() {
//        return Timeout(idleTimeout);
//    }

//    internal static CancellationTokenSource SendTimeout(CancellationToken token) {
//        return Timeout(token, sendTimeout);
//    }

//    internal static CancellationTokenSource ConnectTimeout(CancellationToken token) {
//        return Timeout(token, connectTimeout);
//    }

//    internal static CancellationTokenSource IdleTimeout(CancellationToken token) {
//        return Timeout(token, idleTimeout);
//    }

//    private static CancellationTokenSource Timeout(TimeSpan timeout) {
//        var c = new CancellationTokenSource();
//        c.CancelAfter(timeout);
//        return c;
//    }

//    private static CancellationTokenSource Timeout(CancellationToken token, TimeSpan timeout) {
//        var c = CancellationTokenSource.CreateLinkedTokenSource(token);
//        c.CancelAfter(timeout);
//        return c;
//    }

//}
