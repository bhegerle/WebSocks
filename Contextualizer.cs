using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebStunnel;

internal class Contextualizer {
    private readonly ProtocolByte protoByte;
    private readonly Config config;
    private readonly IPEndPoint socketEndPoint;
    private readonly Uri webSocketUri;

    internal Contextualizer(ProtocolByte protoByte, Config config, CancellationToken crossContextToken) {
        this.protoByte = protoByte;
        this.config = config;
        this.CrossContextToken = crossContextToken;

        if (protoByte == ProtocolByte.WsListener) {
            config.TunnelUri.CheckUri("socket endpoint", "tcp");
            socketEndPoint = config.TunnelUri.EndPoint();
        } else {
            config.TunnelUri.CheckUri("ws url", "ws");
            webSocketUri = config.TunnelUri;
        }
    }

    private CancellationToken CrossContextToken { get; }

    internal IAsyncEnumerable<T> WithReconnectRateLimit<T>(IEnumerable<T> seq) {
        return seq.RateLimited(config.ReconnectDelay, CrossContextToken);
    }

    internal SocketContext Contextualize(SocketId id, Socket s) {
        return new SocketContext(s, id, socketEndPoint, CrossContextToken);
    }

    internal WebSocketContext Contextualize(WebSocket ws) {
        var codec = new Codec(protoByte, config);
        return new WebSocketContext(ws, codec, CrossContextToken);
    }

    internal WebSocketContext Contextualize(ClientWebSocket ws) {
        var codec = new Codec(protoByte, config);
        return new WebSocketContext(ws, webSocketUri, codec, CrossContextToken);
    }
}
