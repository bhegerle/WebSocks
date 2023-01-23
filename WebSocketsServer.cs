using System.Net.Sockets;

namespace WebStunnel;

internal class WebSocketsServer {
    private readonly WebApplication _app;
    private readonly Config _config;

    internal WebSocketsServer(Config config) {
        config.ListenUri.CheckUri("listen", "ws");
        config.TunnelUri.CheckUri("tunnel", "tcp");

        _config = config;

        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(srvOpt => { srvOpt.Listen(config.ListenUri.EndPoint()); });

        _app = builder.Build();
        _app.UseWebSockets();
        _app.Use(Handler);
    }

    private Uri TunnelUri => _config.TunnelUri;

    internal async Task Start() {
        Console.WriteLine($"tunneling {_config.ListenUri} -> {TunnelUri}");
        await _app.RunAsync();
    }

    private async Task Handler(HttpContext ctx, RequestDelegate next) {
        Console.WriteLine($"request from {ctx.GetEndpoint()}");

        if (ctx.WebSockets.IsWebSocketRequest) {
            var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            Console.WriteLine("accepted WebSocket");

            var wsSrc = new SingletonWebSocketSource(ws);
            var b = new Tunnel(ProtocolByte.WsListener, _config, wsSrc);

            await b.Receive(new byte[1000000], Utils.IdleTimeout());
        } else {
            Console.WriteLine("not WebSocket");
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}