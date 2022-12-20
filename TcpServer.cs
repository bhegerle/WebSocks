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

        await Probe.WsHost(TunnelUri, ProxyConfig);

        var tasks = new List<Task>();

        while (socket.Connected)
        {
            var s = await socket.AcceptAsync();

            tasks.Add(HandleConnection(s));

            var i = await Task.WhenAny(tasks);

            try
            {
                await i;
            } catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }

    private async Task HandleConnection(Socket s)
    {
        try
        {
            Console.WriteLine($"connection from {s.RemoteEndPoint}");

            using var ws = new ClientWebSocket();
            ProxyConfig.Configure(ws, TunnelUri);

            await ws.ConnectAsync(TunnelUri, Utils.TimeoutToken());
            Console.WriteLine($"bridging through {TunnelUri}");

            var b = new Bridge(s, ws, ProtocolByte.TcpListener, _config);
            await b.Transit();
        } finally
        {
            Console.WriteLine("done handling connection");
            s.Dispose();
        }
    }
}