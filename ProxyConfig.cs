using System.Net;
using System.Net.WebSockets;

namespace WebStunnel;

public record ProxyConfig
{
    public bool UseSystemProxy { get; init; }
    public string HttpProxy { get; init; }

    internal void Configure(ClientWebSocket ws, Uri uri)
    {
        var proxyUri = TryGetProxyUri(uri);

        if (proxyUri != null)
            ws.Options.Proxy = new WebProxy(proxyUri);
    }

    internal void Configure(HttpClientHandler clientHandler, Uri uri)
    {
        var proxyUri = TryGetProxyUri(uri);

        if (proxyUri != null)
            clientHandler.Proxy = new WebProxy(proxyUri);
    }

    private Uri TryGetProxyUri(Uri uri)
    {
        Uri proxyUri = null;

        if (UseSystemProxy)
        {
            proxyUri = WebRequest.GetSystemWebProxy().GetProxy(uri);

            if (proxyUri != null) Console.WriteLine($"connecting to {uri} through WebProxy {proxyUri}");
        }
        else if (HttpProxy != null)
        {
            proxyUri = new Uri(HttpProxy);
            Console.WriteLine($"connecting through WebProxy {proxyUri}");
        }

        return proxyUri;
    }
}