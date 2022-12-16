using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebStunnel;

internal class TcpServer
{
    private readonly Config _config;

    internal TcpServer(Config config)
    {
        config.ListenUri.CheckUri("listen", "tcp");
        config.TunnelUri.CheckUri("bridge", "ws");
        _config = config;
    }

    private EndPoint EndPoint => _config.ListenUri.EndPoint();
    private Uri TunnelUri => _config.TunnelUri;
    private ProxyConfig ProxyConfig => _config.Proxy;

    internal async Task Start()
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(EndPoint);
        socket.Listen();

        Console.WriteLine($"tunneling {_config.ListenUri} -> {TunnelUri}");

        while (true)
        {
            var s = await socket.AcceptAsync();
            Console.WriteLine($"connection from {s.RemoteEndPoint}");

            var cts = new CancellationTokenSource();

            var ws = new ClientWebSocket();
            ProxyConfig.Configure(ws, TunnelUri);

            await ws.ConnectAsync(TunnelUri, cts.Token);
            Console.WriteLine($"bridging through {TunnelUri}");

            var b = new Bridge(s, ws, _config);
            await b.Transit(cts.Token);
        }
    }
}