namespace WebStunnel;

internal enum LogLevel {
    Error,
    Warn,
    Normal,
    Trace
}

internal static class Log {
    private static readonly SemaphoreSlim mutex;
    private static TextWriter writer;
    private static bool verbose;

    static Log() {
        mutex = new SemaphoreSlim(1);
        writer = Console.Out;
    }

    internal static async Task Configure(Config config) {
        var path = config.LogPath;
        if (path != null) {
            path = Path.GetFullPath(path);

            await Write($"logging to {path}");

            var dir = Path.GetDirectoryName(path);
            if (dir != null)
                Directory.CreateDirectory(dir);
        }

        await mutex.WaitAsync();
        try {
            verbose = config.Verbose;

            if (path != null) {
                var w = new StreamWriter(path) { AutoFlush = true };
                writer = w;
            }
        } finally {
            mutex.Release();
        }
    }

    internal static async Task Write(LogLevel level, params string[] messages) {
        var pfx = level != LogLevel.Normal ? $"{level} ".ToUpper() : null;

        await mutex.WaitAsync();
        try {
            foreach (var m in messages) {
                if (pfx != null)
                    await writer.WriteAsync(pfx);
                await writer.WriteLineAsync(m);
            }
        } finally {
            mutex.Release();
        }
    }

    internal static async Task Error(params string[] messages) {
        await Write(LogLevel.Error, messages);
    }

    internal static async Task Error(string message, Exception e) {
        await Error(message, ExMsg(e));
    }

    internal static async Task Warn(params string[] messages) {
        await Write(LogLevel.Warn, messages);
    }

    internal static async Task Warn(string message, Exception e) {
        await Warn(message, ExMsg(e));
    }

    internal static async Task Write(params string[] messages) {
        await Write(LogLevel.Normal, messages);
    }

    internal static async Task Trace(params string[] messages) {
        if (verbose)
            await Write(LogLevel.Trace, messages);
    }

    private static string ExMsg(Exception e) {
        return verbose ? e.ToString() : $"{e.GetType()}: {e.Message}";
    }
}
