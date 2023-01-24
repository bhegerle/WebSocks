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

    internal AutoconnectWebSocketSource(Config config) {
        tunnelUri = config.TunnelUri;
        proxyConfig = config.Proxy;
    }

    public void Dispose() {
    }

    public async Task<WebSocket> GetWebSocket(CancellationToken token) {
        try {
            var ws = new ClientWebSocket();
            proxyConfig.Configure(ws, tunnelUri);

            Console.WriteLine($"connecting new WebSocket to {tunnelUri}");

            Console.WriteLine(token.WaitHandle.Handle);
            await ws.ConnectAsync(tunnelUri, token);

            Console.WriteLine($"established WebSocket to {tunnelUri}");

            return ws;
        } catch (Exception e) {
            Console.WriteLine(e);
            throw;
        }
    }
}
