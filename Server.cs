using System.Net;
using System.Text.RegularExpressions;
using websocks;

namespace WebSocks;

internal class Server
{
    private readonly string _address;
    private readonly HttpListener _listener;

    internal Server(string address)
    {
        _address = address;
        _listener = new HttpListener();

        var m = Regex.Match(address, "^ws://(.*)");
        if (!m.Success)
            throw new Exception("Only Uris starting with 'ws://' are supported");

        var httpAddress = "http://" + m.Groups[1].Value;
        _listener.Prefixes.Add(httpAddress);
    }

    internal async Task Start()
    {
        _listener.Start();

        Console.WriteLine($"listening on {_address}");

        while (true)
        {
            var ctx = await _listener.GetContextAsync();

            var req = ctx.Request;
            var res = ctx.Response;

            try
            {
                Console.WriteLine($"request from {req.RemoteEndPoint}");

                if (!req.IsWebSocketRequest)
                    throw new Exception("web socket expected");

                var webSock = await ctx.AcceptWebSocketAsync(null);

                var wsRecv = new WebSocketReceiver(webSock.WebSocket);
                await wsRecv.Transit();
            } catch (Exception e)
            {
                res.StatusCode = 500;
                await using var o = new StreamWriter(res.OutputStream);
                await o.WriteAsync(e.ToString());
            }
        }
    }
}