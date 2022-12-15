namespace WebStunnel;

internal class WebSocketsServer
{
    private readonly WebApplication _app;

    internal WebSocketsServer(Config config)
    {
        config.ListenUri.CheckUri("listen", "ws");

        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.WebHost.ConfigureKestrel(srvOpt => { srvOpt.Listen(config.ListenUri.EndPoint()); });

        _app = builder.Build();
        _app.UseWebSockets();
        _app.Use(Handler);
    }

    internal async Task Start()
    {
        await _app.RunAsync();
    }

    private async Task Handler(HttpContext ctx, RequestDelegate next)
    {
        Console.WriteLine($"request from {ctx.GetEndpoint()}");

        if (ctx.WebSockets.IsWebSocketRequest)
        {
            using var webSock = await ctx.WebSockets.AcceptWebSocketAsync();
            Console.WriteLine("accepted WebSocket");

            var s = await Socks4Connector.Connect(webSock, ctx.RequestAborted);

            var b = new Bridge(s, webSock);
            await b.Transit(ctx.RequestAborted);
        }
        else
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}