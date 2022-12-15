using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebSocks;

internal class Socks4Server
{
    private readonly Uri _bridgeUri;
    private readonly EndPoint _endPoint;
    private readonly SystemProxyConfig _sysProxConf;

    internal Socks4Server(Uri socksUri, Uri bridgeUri, SystemProxyConfig sysProxConf)
    {
        socksUri.CheckUri("listen", "socks4");
        bridgeUri.CheckUri("bridge", "ws");

        _endPoint = socksUri.EndPoint();
        _bridgeUri = bridgeUri;
        _sysProxConf = sysProxConf;
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
            _sysProxConf.Configure(ws, _bridgeUri);

            await ws.ConnectAsync(_bridgeUri, cts.Token);
            Console.WriteLine($"bridging through {_bridgeUri}");

            var b = new Bridge(s, ws);
            await b.Transit(cts.Token);
        }
    }
}