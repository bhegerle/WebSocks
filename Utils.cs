using System.Net;

namespace WebSocks;

internal static class Utils
{
    internal static void CheckUri(this Uri uri, string what, string scheme)
    {
        if (uri.Scheme != scheme)
            throw new Exception($"expected {scheme}:// {what} uri");
        if (uri.PathAndQuery != "/")
            throw new Exception($"expected no path and/or query in {what} uri");
        if (!string.IsNullOrEmpty(uri.Fragment))
            throw new Exception($"expected no fragment in {what} uri");
        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new Exception($"expected no user info in {what} uri");
    }

    internal static IPEndPoint EndPoint(this Uri uri)
    {
        var addr = uri.Host == "+" ? IPAddress.Any : IPAddress.Parse(uri.Host);
        return new IPEndPoint(addr, uri.Port);
    }

    internal static Uri ChangeScheme(this Uri uri, string newScheme)
    {
        var s = uri.ToString();
        s = newScheme + s[s.IndexOf(':')..];
        return new Uri(s);
    }
}