using System.Net;

namespace WebSocks;

internal class WebSocketsServer
{
    private readonly HttpListener _listener;
    private readonly Uri _listenUri;

    internal WebSocketsServer(Uri listenUri)
    {
        listenUri.CheckUri("listen", "ws");

        _listenUri = listenUri;
        _listener = new HttpListener();

        var addr = _listenUri.Host;
        var port = _listenUri.Port;

        if (addr == "0.0.0.0")
            addr = "+";

        _listener.Prefixes.Add($"http://{addr}:{port}/");
    }

    internal async Task Start()
    {
        _listener.Start();

        Console.WriteLine($"listening on {_listenUri}");

        while (true)
        {
            var ctx = await _listener.GetContextAsync();
            var req = ctx.Request;
            var res = ctx.Response;

            var cts = new CancellationTokenSource();

            try
            {
                Console.WriteLine($"request from {req.RemoteEndPoint}");

                if (!req.IsWebSocketRequest)
                    throw new Exception("web socket expected");

                var webSock = await ctx.AcceptWebSocketAsync(null);
                Console.WriteLine("accepted WebSocket");

                var s = await Socks4Connector.Connect(webSock.WebSocket, cts.Token);

                var b = new Bridge(s, webSock.WebSocket);
                await b.Transit(cts.Token);
            } catch (Exception e)
            {
                res.StatusCode = 500;
                await using var o = new StreamWriter(res.OutputStream);
                await o.WriteAsync(e.ToString());
            }
        }
    }
}