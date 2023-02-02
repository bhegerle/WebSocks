using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebStunnel;

internal class Contextualizer : IDisposable {
    private readonly Side side;
    private readonly Config config;
    private readonly IPEndPoint socketEndPoint;
    private readonly Uri webSocketUri;
    private readonly CancellationTokenSource cts;

    internal Contextualizer(Side side, Config config, CancellationToken token)
        : this(side, config, CancellationTokenSource.CreateLinkedTokenSource(token)) { }

    internal Contextualizer(Side side, Config config, CancellationTokenSource cts) {
        this.side = side;
        this.config = config;
        this.cts = cts;

        if (side == Side.WsListener) {
            config.TunnelUri.CheckUri("socket endpoint", "tcp");
            socketEndPoint = config.TunnelUri.EndPoint();
        } else {
            config.TunnelUri.CheckUri("ws url", "ws");
            webSocketUri = config.TunnelUri;
        }
    }

    internal CancellationToken Token => cts.Token;

    internal Contextualizer Subcontext(CancellationToken token) {
        var ln = CancellationTokenSource.CreateLinkedTokenSource(Token, token);
        return new Contextualizer(side, config, ln);
    }

    internal IAsyncEnumerable<T> ApplyRateLimit<T>(IEnumerable<T> seq) {
        return seq.RateLimited(config.ReconnectDelay, Token);
    }

    internal SocketContext Contextualize(Socket s) {
        var id = new SocketId();
        return new SocketContext(s, id, null, GetSocketTiming());
    }

    internal SocketContext Contextualize(SocketId id) {
        if (socketEndPoint == null)
            throw new Exception("not configured for socket autoconnect");

        var s = new Socket(SocketType.Stream, ProtocolType.Tcp);
        return new SocketContext(s, id, socketEndPoint, GetSocketTiming());
    }

    internal WebSocketContext Contextualize(WebSocket ws) {
        var codec = new Protocol(side, config);
        return new WebSocketContext(ws, codec, GetSocketTiming());
    }

    internal WebSocketContext Contextualize(ClientWebSocket ws) {
        var codec = new Protocol(side, config);
        return new WebSocketContext(ws, webSocketUri, codec, GetSocketTiming());
    }

    private SocketTiming GetSocketTiming() {
        var t = CancellationTokenSource.CreateLinkedTokenSource(Token);
        return new SocketTiming(config, t);
    }

    public void Dispose() {
        cts.Dispose();
    }
}
