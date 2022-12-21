using System.Net;
using System.Net.WebSockets;

namespace WebStunnel;

public record ProxyConfig {
    public bool UseSystemProxy { get; init; }
    public string HttpProxy { get; init; }

    internal void Configure(ClientWebSocket ws, Uri uri) {
        var proxy = TryGetProxy(uri);

        if (proxy != null)
            ws.Options.Proxy = proxy;
    }

    internal void Configure(HttpClientHandler clientHandler, Uri uri) {
        var proxy = TryGetProxy(uri);

        if (proxy != null)
            clientHandler.Proxy = proxy;
    }

    private WebProxy TryGetProxy(Uri uri) {
        if (UseSystemProxy) {
            var proxyUri = WebRequest.GetSystemWebProxy().GetProxy(uri);

            if (proxyUri != null) {
                Console.WriteLine($"connecting to {uri} through WebProxy {proxyUri}");
                return new WebProxy(proxyUri) { UseDefaultCredentials = true };
            }
        } else if (HttpProxy != null) {
            var proxyUri = new Uri(HttpProxy);
            Console.WriteLine($"connecting through WebProxy {proxyUri}");
            return new WebProxy(proxyUri);
        }

        return null;
    }
}