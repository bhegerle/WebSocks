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
        CrossContextToken = crossContextToken;

        if (protoByte == ProtocolByte.WsListener) {
            config.TunnelUri.CheckUri("socket endpoint", "tcp");
            socketEndPoint = config.TunnelUri.EndPoint();
        } else {
            config.TunnelUri.CheckUri("ws url", "ws");
            webSocketUri = config.TunnelUri;
        }
    }

#warning can this be made private?
    internal CancellationToken CrossContextToken { get; }

    internal CancellationTokenSource Link() {
        return CancellationTokenSource.CreateLinkedTokenSource(CrossContextToken);
    }

    internal CancellationTokenSource Link(CancellationToken token) {
        return CancellationTokenSource.CreateLinkedTokenSource(CrossContextToken, token);
    }

    internal IAsyncEnumerable<T> ApplyRateLimit<T>(IEnumerable<T> seq) {
        return seq.RateLimited(config.ReconnectDelay, CrossContextToken);
    }

    internal SocketContext Contextualize(SocketId id, Socket s) {
        return new SocketContext(s, id, socketEndPoint, this);
    }

    internal WebSocketContext Contextualize(WebSocket ws) {
        var codec = new Codec(protoByte, config);
        return new WebSocketContext(ws, codec, this);
    }

    internal WebSocketContext Contextualize(ClientWebSocket ws) {
        var codec = new Codec(protoByte, config);
        return new WebSocketContext(ws, webSocketUri, codec, this);
    }

    internal CancellationTokenSource ConnectTimeout() {
        return Timeout(config.ConnectTimeout);
    }

    private CancellationTokenSource Timeout(TimeSpan t) {
        var c = Link();
        c.CancelAfter(t);
        return c;
    }
}
