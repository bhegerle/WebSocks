using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using websocks;

namespace WebSocks;

internal class Client
{
    private readonly EndPoint _endPoint;
    private readonly Uri _bridgeUri;

    internal Client(Uri socksUri, Uri bridgeUri)
    {
        socksUri.CheckScheme("listen", "socks4");
        bridgeUri.CheckScheme("listen", "ws");

        _endPoint = socksUri.EndPoint();
        _bridgeUri = bridgeUri;
    }

    internal async Task Start()
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(_endPoint);
        socket.Listen();
        Console.WriteLine($"listening on {_endPoint}");

        while (true)
        {
            var s = await socket.AcceptAsync();
            Console.WriteLine($"connection from {s.RemoteEndPoint}");

            var cts = new CancellationTokenSource();

            var ws = new ClientWebSocket();
            await ws.ConnectAsync(_bridgeUri, cts.Token);
            Console.WriteLine($"bridging through {_bridgeUri}");

            var b = new Bridge(s, ws);
            await b.Transit(cts.Token);
        }
    }
}