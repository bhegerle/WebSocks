namespace WebStunnel;

internal static class Control {
    internal static async Task Transfer(CancellationTokenSource cts) {
        await Log.Write((LogLevel?)null, "type '.' to exit");
        while (true) {
            var line = await Console.In.ReadLineAsync();
            if (line == ".")
                break;
        }

        cts.Cancel();
        await Log.Write("shutting down");
    }
}
