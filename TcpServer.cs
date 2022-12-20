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

        await Test();

        while (true)
        {
            var s = await socket.AcceptAsync();
            Console.WriteLine($"connection from {s.RemoteEndPoint}");

            var ws = new ClientWebSocket();
            ProxyConfig.Configure(ws, TunnelUri);

            try
            {
                await ws.ConnectAsync(TunnelUri, Utils.TimeoutToken());
                Console.WriteLine($"bridging through {TunnelUri}");
            } catch (Exception e)
            {
                Console.WriteLine($"could not connect to {TunnelUri}: {e.Message}");
                s.ForceClose();
                continue;
            }

            var b = new Bridge(s, ws, ProtocolByte.TcpListener, _config);
            await b.Transit();
        }
    }

    private async Task Test()
    {
        var testUri = new UriBuilder
        {
            Scheme = "http",
            Host = TunnelUri.Host,
            Port = TunnelUri.Port
        }.Uri;

        Console.WriteLine($"testing connection to {testUri}");

        var hch = new HttpClientHandler();
        ProxyConfig.Configure(hch, testUri);

        using var client = new HttpClient(hch);

        var res = await client.GetAsync(testUri);
    }
}