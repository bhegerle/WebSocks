using System.Net;
using websocks;

namespace WebSocks;

internal class Server
{
    private readonly HttpListener _listener;
    private readonly Uri _listenUri;

    internal Server(Uri listenUri)
    {
        _listenUri = listenUri;
        _listener = new HttpListener();

        var httpUri = _listenUri.ChangeScheme("http");
        _listener.Prefixes.Add(httpUri.ToString());
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