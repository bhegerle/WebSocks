using System.Net;
namespace WebStunnel;

internal class ServerContext {
    private readonly Side side;
    private readonly IPEndPoint sockEp;
    private readonly Uri wsUri;

    internal ServerContext(Side side, Config config, CancellationToken token) {
        this.side = side;
        this.Config = config;
        Token = token;

        if (side == Side.WsListener) {
            config.TunnelUri.CheckUri("socket endpoint", "tcp");
            sockEp = config.TunnelUri.EndPoint();
        } else {
            config.TunnelUri.CheckUri("ws url", "ws");
            wsUri = config.TunnelUri;
        }
    }

    internal Config Config { get; }
    internal CancellationToken Token { get; }

    internal IPEndPoint SocketEndPoint {
        get {
            if (sockEp == null)
                throw new Exception("not configured for socket autoconnect");
            return sockEp;
        }
    }

    internal Uri WebSocketUri {
        get {
            if (wsUri == null)
                throw new Exception("not configured for WebSocket autoconnect");
            return wsUri;
        }
    }

    internal CancellationTokenSource Link() {
        return CancellationTokenSource.CreateLinkedTokenSource(Token);
    }

    internal Protocol MakeProtocol() {
        return new Protocol(side, Config);
    }
}
