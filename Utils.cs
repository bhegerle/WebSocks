using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebStunnel;

internal static class Utils {
    internal static void CheckUri(this Uri uri, string what, string scheme) {
        if (uri.Scheme != scheme)
            throw new Exception($"expected {scheme}:// {what} uri");
        if (!string.IsNullOrEmpty(uri.Query))
            throw new Exception($"expected no query in {what} uri");
        if (!string.IsNullOrEmpty(uri.Fragment))
            throw new Exception($"expected no fragment in {what} uri");
        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new Exception($"expected no user info in {what} uri");
    }

    internal static bool ConjEqual(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b) {
        var eq = true;
        for (var i = 0; i < a.Length && i < b.Length; i++) eq = eq && a[i] == b[i];
        return eq;
    }

    internal static IPEndPoint EndPoint(this Uri uri) {
        var addr = uri.Host == "+" ? IPAddress.Any : IPAddress.Parse(uri.Host);
        return new IPEndPoint(addr, uri.Port);
    }

    internal static ArraySegment<byte> AsSegment(this byte[] x) {
        return new ArraySegment<byte>(x);
    }

    internal static ArraySegment<byte> AsSegment(this byte[] x, int offset, int count) {
        return new ArraySegment<byte>(x, offset, count);
    }

    internal static CancellationToken TimeoutToken(bool longTimout = true) {
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(longTimout ? 2000 : 100));
        return cts.Token;
    }

    internal static CancellationToken IdleTimeout() { 
        var cts = new CancellationTokenSource();
        cts.CancelAfter(Config.IdleTimeout);
        return cts.Token;
    }

    internal static CancellationToken TimeoutToken() { 
        var cts = new CancellationTokenSource();
        cts.CancelAfter(Config.Timeout);
        return cts.Token;
    }

    internal static void ForceClose(this Socket s) {
        try {
            if (s.Connected) s.Close(100);
        } catch {
            // ignored
        }
    }

    internal static async Task ForceCloseAsync(this WebSocket ws) {
        try {
            if (ws.State != WebSocketState.Closed)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, TimeoutToken(false));
        } catch {
            // ignored
        }
    }

    public static void SetLogPath(string path) {
        path = Path.GetFullPath(path);

        Console.WriteLine($"logging to {path}");

        var dir = Path.GetDirectoryName(path);
        if (dir != null)
            Directory.CreateDirectory(dir);

        var w = new StreamWriter(path) { AutoFlush = true };
        Console.SetOut(w);
    }
}