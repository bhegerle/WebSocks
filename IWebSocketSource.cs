using System.Net.WebSockets;
using System.Runtime.CompilerServices;

namespace WebStunnel;

internal class WebSocketConnector {
    private readonly Uri uri;
    private readonly ProxyConfig proxyConfig;
    private readonly TimeSpan delay;

    internal WebSocketConnector(Config config) {
        uri = config.TunnelUri;
        proxyConfig = config.Proxy;
        delay = config.ReconnectDelay;
    }

    internal static IAsyncEnumerable<WebSocket> Singleton(WebSocket ws) {
        return new[] { ws }.AsAsync();
    }

    internal async IAsyncEnumerable<WebSocket> Autoreconnect([EnumeratorCancellation] CancellationToken token = default) {
        var seq = Constructed().RateLimited(delay,token);
        await foreach (var ws in seq)
            yield return ws;
    }

    private IEnumerable<ClientWebSocket> Constructed() {
        while (true) {
            var ws = new ClientWebSocket();

            try {
                proxyConfig.Configure(ws, uri);
            } catch (Exception e) {
                throw new Exception("failed to configure ws proxy settings", e);
            }

            yield return ws;
        }
    }

    private async IAsyncEnumerable<ClientWebSocket> Connected(IAsyncEnumerable<ClientWebSocket> seq,
        [EnumeratorCancellation] CancellationToken token = default) {
        await foreach (var ws in seq.WithCancellation(token)) {
            await Log.Write($"connecting to {uri}");

            Exception conEx = null;
            try {
                await ws.ConnectAsync(uri, token);
            } catch (OperationCanceledException) {
                yield break;
            } catch (Exception e) {
                conEx = e;
            }

            if (conEx == null) {
                yield return ws;
            } else {
                ws.Dispose();
                await Log.Write($"could not connect to {uri}", conEx);
            }
        }
    }
}

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

        await Log.Write($"connecting to {tunnelUri}");

        try {
            await ws.ConnectAsync(tunnelUri, token);
        } catch (Exception e) {
            throw new Exception($"could not connect to {tunnelUri}", e);
        }

        return ws;
    }
}
