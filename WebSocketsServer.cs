using System.Net.Sockets;

namespace WebStunnel;

internal class WebSocketsServer {
    private readonly WebApplication app;
    private readonly Config config;

    internal WebSocketsServer(Config config) {
        config.ListenUri.CheckUri("listen", "ws");
        config.TunnelUri.CheckUri("tunnel", "tcp");

        this.config = config;

        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(srvOpt => { srvOpt.Listen(config.ListenUri.EndPoint()); });

        app = builder.Build();
        app.UseWebSockets();
        app.Use(Handler);
    }

    private Uri TunnelUri => config.TunnelUri;

    internal async Task Start() {
        Console.WriteLine($"tunneling {config.ListenUri} -> {TunnelUri}");
        await app.RunAsync();
    }

    private async Task Handler(HttpContext ctx, RequestDelegate next) {
        Console.WriteLine($"request from {ctx.GetEndpoint()}");

        if (ctx.WebSockets.IsWebSocketRequest) {
            var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            Console.WriteLine("accepted WebSocket");

            var wsSrc = new SingletonWebSocketSource(ws);
            var b = new Tunnel(ProtocolByte.WsListener, config, wsSrc);

            await b.Receive(new byte[1000000], Utils.IdleTimeout());
        } else {
            Console.WriteLine("not WebSocket");
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}