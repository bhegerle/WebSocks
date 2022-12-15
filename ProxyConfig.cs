using System.Net;
using System.Net.WebSockets;

namespace WebSocks;

public record ProxyConfig
{
    public bool UseSystemProxy { get; init; } = true;
    public string HttpProxy { get; init; }

    public void Configure(ClientWebSocket ws, Uri uri)
    {
        if (UseSystemProxy)
        {
            var proxyUri = WebRequest.GetSystemWebProxy().GetProxy(uri);
            ws.Options.Proxy = new WebProxy(proxyUri);
            Console.WriteLine($"connecting to {uri} through WebProxy {proxyUri}");
        }
        else if (HttpProxy != null)
        {
            var proxyUri = new Uri(HttpProxy);
            ws.Options.Proxy = new WebProxy(proxyUri);
            Console.WriteLine($"connecting through WebProxy {proxyUri}");
        }
    }
}