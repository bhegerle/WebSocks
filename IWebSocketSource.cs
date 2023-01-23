using System.Net.WebSockets;

namespace WebStunnel;

public interface IWebSocketSource : IDisposable {
    Task<WebSocket> GetWebSocket(CancellationToken token);
}

internal class SingletonWebSocketSource : IWebSocketSource {
    private WebSocket _ws;

    internal SingletonWebSocketSource(WebSocket ws) {
        _ws = ws;
    }

    public void Dispose() {
        using (_ws)
            return;
    }

    public Task<WebSocket> GetWebSocket(CancellationToken token) {
        var t = Task.FromResult(_ws);
        _ws = null;
        return t;
    }
}

internal class AutoconnectWebSocketSource : IWebSocketSource {
    private readonly Uri _tunnelUri;
    private readonly ProxyConfig _proxyConfig;

    internal AutoconnectWebSocketSource(Uri tunnelUri, ProxyConfig proxyConfig) {
        _tunnelUri = tunnelUri;
        _proxyConfig = proxyConfig;
    }

    public void Dispose() {
    }

    public async Task<WebSocket> GetWebSocket(CancellationToken token) {
        var ws = new ClientWebSocket();
        _proxyConfig.Configure(ws, _tunnelUri);

        Console.WriteLine($"connecting new WebSocket to {_tunnelUri}");

        await ws.ConnectAsync(_tunnelUri, token);

        return ws;
    }
}
