using System.Net.WebSockets;

namespace WebStunnel;

public interface IWebSocketSource {
    Task<WebSocket> GetWebSocket(CancellationToken token);
}

internal class SingletonWebSocketSource : IWebSocketSource {
    private WebSocket ws;

    internal SingletonWebSocketSource(WebSocket ws) {
        this.ws = ws;
    }

    public Task<WebSocket> GetWebSocket(CancellationToken token) {
        var t = Task.FromResult(ws);
        ws = null;
        return t;
    }
}

internal class AutoconnectWebSocketSource : IWebSocketSource {
    private readonly Uri tunnelUri;
    private readonly ProxyConfig proxyConfig;

    internal AutoconnectWebSocketSource(Config config) {
        tunnelUri = config.TunnelUri;
        proxyConfig = config.Proxy;
    }

    public async Task<WebSocket> GetWebSocket(CancellationToken token) {
        var ws = new ClientWebSocket();
        proxyConfig.Configure(ws, tunnelUri);

        Console.WriteLine($"connecting to {tunnelUri}");
        await ws.ConnectAsync(tunnelUri, token);

        return ws;
    }
}
