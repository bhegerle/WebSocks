namespace WebStunnel;

internal static class Control {
    private static int cancelCount;
    private static readonly Timer exitTimer;

    static Control() {
        exitTimer = new Timer(_ => ExitNow(), null, Timeout.Infinite, Timeout.Infinite);
    }

    internal static async Task Transfer(CancellationTokenSource cts) {
        Console.CancelKeyPress += (_, evt) => {
            if (Interlocked.Increment(ref cancelCount) == 1) {
                cts.Cancel();
                exitTimer.Change(1000, Timeout.Infinite);
            } else {
                ExitNow();
            }
        };

        try {
            await Task.Delay(Timeout.Infinite, cts.Token);
        } catch (OperationCanceledException) {
            await Log.Write("shutting down");
        }
    }

    private static void ExitNow() {
        Console.WriteLine("terminating");
        Environment.Exit(9);
    }
}
