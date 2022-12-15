using System.Net;
using System.Net.WebSockets;

namespace WebSocks;

internal class SystemProxyConfig
{
    private readonly bool _useSystemProxy;

    internal SystemProxyConfig(bool useSystemProxy)
    {
        _useSystemProxy = useSystemProxy;
    }

    public void Configure(ClientWebSocket ws, Uri uri)
    {
        if (_useSystemProxy)
        {
            var proxyUri = WebRequest.GetSystemWebProxy().GetProxy(uri);
            ws.Options.Proxy = new WebProxy(proxyUri);
            Console.WriteLine($"connecting to {uri} through WebProxy {proxyUri}");
        }
    }
}