using System.Net;

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

    internal static IPEndPoint EndPoint(this Uri uri) {
        var addr = uri.Host == "+" ? IPAddress.Any : IPAddress.Parse(uri.Host);
        return new IPEndPoint(addr, uri.Port);
    }

    internal static ArraySegment<byte> Extend(this ArraySegment<byte> x, int extensionCount) {
        if (x.Array == null)
            throw new Exception("cannot extend null array");
        return new ArraySegment<byte>(x.Array, x.Offset, x.Count + extensionCount);
    }
}