namespace WebStunnel;

internal static class Control {
    internal static async Task RunUntilCancelled(CancellationTokenSource cts) {
        await Log.Write((LogLevel?)null, "type '.' to exit");
        while (true) {
            var line = await Console.In.ReadLineAsync();
            if (line == ".")
                break;
        }

        await Log.Write("cancelling io tasks");
        cts.Cancel();
    }
}
