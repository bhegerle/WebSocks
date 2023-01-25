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

        try {
            proxyConfig.Configure(ws, tunnelUri);
        } catch (Exception e) {
            throw new Exception("failed to configure ws proxy settings", e);
        }

        Console.WriteLine($"connecting to {tunnelUri}");

        try {
            await ws.ConnectAsync(tunnelUri, token);
        } catch (Exception e) {
            throw new Exception($"could not connect to {tunnelUri}", e);
        }

        return ws;
    }
}
