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
                var id = new SocketId();

                Console.WriteLine($"accepted connection {id} from {s.RemoteEndPoint}");

                await sockMap.AddSocket(id, s);
            }
        } catch (OperationCanceledException) {
            Console.WriteLine("cancelled socket accept");
            throw;
        } catch (Exception e) {
            Console.WriteLine("unexpected socket accept exception");
            Console.WriteLine(e);
            throw;
        } finally {
            Console.WriteLine("done accepting");
        }
    }

    private async Task Multiplex(CancellationToken token) {
        try {
            var wsSrc = new AutoconnectWebSocketSource(config);
            var channelCon = new ChannelConnector(ProtocolByte.TcpListener, config, wsSrc);
            var multiplexer = new Multiplexer(channelCon, sockMap);

            await multiplexer.Multiplex(token);
        } catch (OperationCanceledException) {
            Console.WriteLine("cancelled multiplexing");
            throw;
        } catch (Exception e) {
            Console.WriteLine("unexpected multiplexing exception");
            Console.WriteLine(e);
            throw;
        } finally {
            Console.WriteLine("done multiplexing");
        }
    }
}