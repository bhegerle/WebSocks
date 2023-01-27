using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebStunnel;

internal class Contextualizer {
    private readonly ProtocolByte protoByte;
    private readonly Config config;
    private readonly IPEndPoint socketEndPoint;
    private readonly Uri webSocketUri;
    private readonly CancellationToken crossContextToken;

    internal Contextualizer(ProtocolByte protoByte, Config config, CancellationToken crossContextToken) {
        this.protoByte = protoByte;
        this.config = config;
        this.crossContextToken = crossContextToken;

        if (protoByte == ProtocolByte.WsListener) {
            config.TunnelUri.CheckUri("socket endpoint", "tcp");
            socketEndPoint = config.TunnelUri.EndPoint();
        } else {
            config.TunnelUri.CheckUri("ws url", "ws");
            webSocketUri = config.TunnelUri;
        }
    }

    internal CancellationTokenSource Link() {
        return CancellationTokenSource.CreateLinkedTokenSource(crossContextToken);
    }

    internal CancellationTokenSource Link(CancellationToken token) {
        return CancellationTokenSource.CreateLinkedTokenSource(crossContextToken, token);
    }

    internal IAsyncEnumerable<T> ApplyRateLimit<T>(IEnumerable<T> seq) {
        return seq.RateLimited(config.ReconnectDelay, crossContextToken);
    }

    internal SocketContext Contextualize(SocketId id, Socket s) {
        return new SocketContext(s, id, socketEndPoint, GetSocketCancellation());
    }

    internal WebSocketContext Contextualize(WebSocket ws) {
        var codec = new Codec(protoByte, config);
        return new WebSocketContext(ws, codec, GetSocketCancellation());
    }

    internal WebSocketContext Contextualize(ClientWebSocket ws) {
        var codec = new Codec(protoByte, config);
        return new WebSocketContext(ws, webSocketUri, codec, GetSocketCancellation());
    }

    internal SocketCancellation GetSocketCancellation() {
        return new SocketCancellation(config, Link());
    }
}
