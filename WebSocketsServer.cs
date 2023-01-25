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

    internal async Task Start(CancellationToken token) {
        await Log.Write($"tunneling {config.ListenUri} -> {TunnelUri}");
        await app.StartAsync();

        try {
            await Task.Delay(Timeout.Infinite, token);
        } finally {
            await Log.Write("cancelling ws tasks");
            await app.StopAsync();
        }
    }

    private async Task Handler(HttpContext ctx, RequestDelegate next) {
        await Log.Write($"request from {ctx.GetEndpoint()}");

        if (ctx.WebSockets.IsWebSocketRequest) {
            var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            await Log.Write("accepted WebSocket");

            var wsSrc = new SingletonWebSocketSource(ws);
            var channelCon = new ChannelConnector(ProtocolByte.WsListener, config, wsSrc);
            var sockMap = new AutoconnectSocketMap(config.TunnelUri.EndPoint());

            var multiplexer = new Multiplexer(channelCon, sockMap);

            await multiplexer.Multiplex(ctx.RequestAborted);
        } else {
            await Log.Warn("not WebSocket");
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}