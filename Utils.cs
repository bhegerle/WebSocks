using System.Net;

namespace WebSocks;

internal static class Utils
{
    internal static void CheckScheme(this Uri uri, string what, string scheme)
    {
        if (uri.Scheme != scheme)
            throw new Exception($"expected {scheme}:// {what} uri");
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