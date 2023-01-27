namespace WebStunnel;

internal class WebSocketsServer : IServer {
    private readonly CancellationTokenSource cts;
    private readonly WebApplication app;
    private readonly Config config;

    internal WebSocketsServer(Config config) {
        config.ListenUri.CheckUri("listen", "ws");
        config.TunnelUri.CheckUri("tunnel", "tcp");

        this.config = config;

        cts = new CancellationTokenSource();

        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(srvOpt => srvOpt.Listen(config.ListenUri.EndPoint()));

        app = builder.Build();
        app.UseWebSockets();
        app.Use(Handler);
    }

    private Uri TunnelUri => config.TunnelUri;

    public async Task Start(CancellationToken token) {
        await Log.Write($"tunneling {config.ListenUri} -> {TunnelUri}");
        await app.StartAsync(token);

        try {
            await Task.Delay(Timeout.Infinite, token);
        } finally {
            await Log.Write("cancelling ws tasks");
            cts.Cancel();

            await Stop();
        }
    }

    private async Task Stop() {
        try {
            using var stop = new CancellationTokenSource();
            stop.CancelAfter(500);
            await app.StopAsync(stop.Token);
        } catch {
            // ignored
        }
    }

    public async ValueTask DisposeAsync() {
        await app.DisposeAsync();
        cts.Dispose();
    }

    private async Task Handler(HttpContext ctx, RequestDelegate next) {
        await Log.Write($"request from {ctx.GetEndpoint()}");

        if (ctx.WebSockets.IsWebSocketRequest) {
            var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            await Log.Write("accepted WebSocket");

            var wsSrc = new SingletonWebSocketSource(ws);
            var wsSngl = WebSocketConnector.Singleton(ws);
            var channelCon = new ChannelConnector(ProtocolByte.WsListener, config, wsSrc, wsSngl);
            var sockMap = new AutoconnectSocketMap(config.TunnelUri.EndPoint());

            using var multiplexer = new Multiplexer(channelCon, sockMap);

            await multiplexer.Multiplex(cts.Token);
        } else {
            await Log.Warn("not WebSocket");
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}