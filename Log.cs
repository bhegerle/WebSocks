namespace WebStunnel;

internal enum LogLevel {
    Error,
    Warn,
    Info
}

internal static class Log {
    private static readonly SemaphoreSlim mutex;
    private static TextWriter writer;

    static Log() {
        mutex = new SemaphoreSlim(1);
        writer = Console.Out;
    }

    internal static async Task SetLogPath(string path) {
        path = Path.GetFullPath(path);

        await Write($"logging to {path}");

        var dir = Path.GetDirectoryName(path);
        if (dir != null)
            Directory.CreateDirectory(dir);

        var w = new StreamWriter(path) { AutoFlush = true };

        await mutex.WaitAsync();
        try {
            writer = w;
        } finally {
            mutex.Release();
        }
    }

    internal static async Task Write(LogLevel? level, params string[] messages) {
        var pfx = level != null ? $"{level} ".ToUpper() : null;

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
        var emsg = $"{e.GetType()}: {e.Message}";
        await Error(message, emsg);
    }

    internal static async Task Warn(params string[] messages) {
        await Write(LogLevel.Warn, messages);
    }

    internal static async Task Warn(string message, Exception e) {
        var emsg = $"{e.GetType()}: {e.Message}";
        await Warn(message, emsg);
    }

    internal static async Task Write(params string[] messages) {
        await Write(null, messages);
    }
}
