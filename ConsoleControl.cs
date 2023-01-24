namespace WebStunnel;

internal static class ConsoleControl {
    internal static async Task Run() {
        await Console.Out.WriteLineAsync("type '.' to exit");
        while (true) {
            var line = await Console.In.ReadLineAsync();
            if (line == ".")
                break;
        }
    }
}
