using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace WebStunnel;

internal class TcpServer : IServer {
    private readonly Config config;
    private readonly SocketMap sockMap;
    private readonly Multiplexer multiplexer;

    internal TcpServer(Config config) {
        config.ListenUri.CheckUri("listen", "tcp");
        config.TunnelUri.CheckUri("bridge", "ws");

        this.config = config;

        sockMap = new SocketMap();

        var wsSrc = new AutoconnectWebSocketSource(config);
        var wsArc = new WebSocketConnector(config).Autoreconnect();
        var channelCon = new ChannelConnector(ProtocolByte.TcpListener, config, wsSrc, wsArc);
        multiplexer = new Multiplexer(channelCon, sockMap);
    }

    public async Task Start(CancellationToken token) {
        await Log.Write($"tunneling {config.ListenUri} -> {config.TunnelUri}");

        var sockMap2 = new SocketMap2(ctx, Resolve);
        var ctx = new Contextualizer(ProtocolByte.TcpListener, config, token);

        var at = AcceptLoop(token);
        var tt = Multiplex(sockMap2, ctx);
        await Task.WhenAll(at, tt);
    }

    public ValueTask DisposeAsync() {
        sockMap.Dispose();
        multiplexer.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task AcceptLoop(SocketMap2 sockMap, Contextualizer ctx) {
        try {
            using var listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(config.ListenUri.EndPoint());
            listener.Listen();

            await Log.Write($"listening on {listener.LocalEndPoint}");

            while (true) {
                var s = await listener.AcceptAsync(token);

                await Log.Write($"accepted connection {id} from {s.RemoteEndPoint}");

                await sockMap.AddSocket(id, s);
            }
        } catch (OperationCanceledException) {
            await Log.Write("cancelled socket accept");
            throw;
        } catch (Exception e) {
            await Log.Warn("unexpected socket accept exception", e);
            throw;
        }
    }

    private async Task Multiplex(SocketMap2 sockMap, Contextualizer ctx) {
        try {
            await Multiplexer.Multiplex(Repeatedly.Invoke(x), sockMap, ctx);
        } catch (OperationCanceledException) {
            await Log.Write("cancelled multiplexing");
            throw;
        } catch (Exception e) {
            await Log.Warn("unexpected multiplexing exception", e);
            throw;
        }
    }

    private ClientWebSocket x() {
        var ws = new ClientWebSocket();

        try {
            config.Proxy.Configure(ws, config.TunnelUri);
        } catch (Exception e) {
            throw new Exception("failed to configure ws proxy settings", e);
        }

        return ws;
    }

    private Task<Socket> Resolve(SocketId id, CancellationToken token) {
        return Task.FromException<Socket>(new Exception($"no socket with id {id}"));
    }
}