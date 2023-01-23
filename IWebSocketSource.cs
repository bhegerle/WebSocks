using System.Net.WebSockets;

namespace WebStunnel;

public interface IWebSocketSource : IDisposable {
    Task<WebSocket> GetWebSocket(CancellationToken token);
}

internal class SingletonWebSocketSource : IWebSocketSource {
    private WebSocket ws;

    internal SingletonWebSocketSource(WebSocket ws) {
        this.ws = ws;
    }

    public void Dispose() {
        using (ws)
            return;
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

    internal AutoconnectWebSocketSource(Uri tunnelUri, ProxyConfig proxyConfig) {
        this.tunnelUri = tunnelUri;
        this.proxyConfig = proxyConfig;
    }

    public void Dispose() {
    }

    public async Task<WebSocket> GetWebSocket(CancellationToken token) {
        var ws = new ClientWebSocket();
        proxyConfig.Configure(ws, tunnelUri);

        Console.WriteLine($"connecting new WebSocket to {tunnelUri}");

        await ws.ConnectAsync(tunnelUri, token);

        return ws;
    }
}
