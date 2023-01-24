using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;
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
        for (var i = 0; i < a.Length && i < b.Length; i++)
            eq = (eq && a[i] == b[i]);
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

    internal static ArraySegment<byte> Extend(this ArraySegment<byte> x, int extensionCount) {
        return new ArraySegment<byte>(x.Array, x.Offset, x.Offset + extensionCount);
    }

#warning this is suspect
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

    internal static async Task UntilCancelled(this Task t, CancellationToken token) {
        var delay = Task.Delay(Timeout.Infinite, token);
        await Task.WhenAny(t, delay);
        await t;
    }

    internal static async Task<bool> DidCompleteWithin(this Task t, TimeSpan timeout) {
        var delay = Task.Delay(timeout);
        var done = await Task.WhenAny(t, delay);
        return ReferenceEquals(t, done);
    }

    internal static CancellationTokenSource Link(this CancellationToken token0, CancellationToken token1) {
        return CancellationTokenSource.CreateLinkedTokenSource(token0, token1);
    }

    internal static async Task Send(this Socket s, ArraySegment<byte> seg, CancellationToken token) {
        await s.SendAsync(seg, SocketFlags.None, token);
    }

    internal static async Task<ArraySegment<byte>> Receive(this Socket s, ArraySegment<byte> seg, CancellationToken token) {
        var n = await s.ReceiveAsync(seg, SocketFlags.None, token);
        return seg[..n];
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

    internal static void SetLogPath(string path) {
        path = Path.GetFullPath(path);

        Console.WriteLine($"logging to {path}");

        var dir = Path.GetDirectoryName(path);
        if (dir != null)
            Directory.CreateDirectory(dir);

        var w = new StreamWriter(path) { AutoFlush = true };
        Console.SetOut(w);
    }
}