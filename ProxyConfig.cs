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

    private WebProxy TryGetProxy(Uri uri) {
        if (UseSystemProxy) {
            var proxyUri = WebRequest.GetSystemWebProxy().GetProxy(uri);

            if (proxyUri != null) {
                Log.Write($"connecting to {uri} through WebProxy {proxyUri}").Wait();
                return new WebProxy(proxyUri) { UseDefaultCredentials = true };
            }
        } else if (HttpProxy != null) {
            var proxyUri = new Uri(HttpProxy);
            Log.Write($"connecting through WebProxy {proxyUri}").Wait();
            return new WebProxy(proxyUri);
        }

        return null;
    }
}