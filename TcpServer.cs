using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;

namespace WebStunnel;

internal class TcpServer {
    private readonly Config config;
    private readonly SocketMap sockMap;

    internal TcpServer(Config config) {
        config.ListenUri.CheckUri("listen", "tcp");
        config.TunnelUri.CheckUri("bridge", "ws");
        this.config = config;
        
        sockMap = new SocketMap();
    }

    internal async Task Start(CancellationToken token) {
        var at = AcceptLoop(token);
        var tt = Multiplex(token);
        await Task.WhenAll(at, tt);
    }

    private async Task AcceptLoop(CancellationToken token) {
        try {
            using var rng = RandomNumberGenerator.Create();

            using var listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(config.ListenUri.EndPoint());
            listener.Listen();

            Console.WriteLine($"listening on {listener.LocalEndPoint}");

            while (true) {
                var s = await listener.AcceptAsync(token);
                var id = GetId(rng);

                Console.WriteLine($"accepted connection {id} from {s.RemoteEndPoint}");

                await sockMap.AddSocket(id, s);
            }
        } catch (OperationCanceledException) {
            Console.WriteLine("cancelled socket accept");
        }
    }

    private async Task Multiplex(CancellationToken token) {
        try {
            var wsSrc = new AutoconnectWebSocketSource(config);
            var tunnel = new Tunnel(ProtocolByte.TcpListener, config, wsSrc);
          using    var multiplexer = new Multiplexer(tunnel, sockMap);

            await multiplexer.Multiplex(token);
        } catch (OperationCanceledException) {
            Console.WriteLine("cancelled tunneling");
        }
    }

    private static ulong GetId(RandomNumberGenerator rng) {
        var b = new byte[sizeof(ulong)];
        rng.GetNonZeroBytes(b);
        return BitConverter.ToUInt64(b);
    }
}