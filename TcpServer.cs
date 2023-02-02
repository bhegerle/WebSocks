using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace WebStunnel;

internal class TcpServer : IServer {
    private readonly Config config;

    internal TcpServer(Config config) {
        config.ListenUri.CheckUri("listen", "tcp");
        config.TunnelUri.CheckUri("bridge", "ws");

        this.config = config;
    }

    public async Task Start(CancellationToken token) {
        await Log.Write($"tunneling {config.ListenUri} -> {config.TunnelUri}");

        using var listener = await CreateListener();

        using var ctx = new Contextualizer(Side.TcpListener, config, token);
        using var sockMap = new SocketMap();

        var at = AcceptLoop(listener, sockMap, ctx);
        var tt = Multiplex(sockMap, ctx);

        await Task.WhenAll(at, tt);
    }

    public ValueTask DisposeAsync() {
        return ValueTask.CompletedTask;
    }

    private async Task<Socket> CreateListener() {
        var listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(config.ListenUri.EndPoint());
        listener.Listen();

        await Log.Write($"listening on {listener.LocalEndPoint}");

        return listener;
    }

    private static async Task AcceptLoop(Socket listener, SocketMap sockMap, Contextualizer ctx) {
        try {
            while (true) {
                await Log.Write("awaiting");
                var s = await listener.AcceptAsync(ctx.Token);

                var sctx = ctx.Contextualize(s);

                await Log.Write($"accepted connection {sctx.Id} from {s.RemoteEndPoint}");

                await sockMap.Add(sctx);
            }
        } catch (Exception e) {
            await Log.Warn("unexpected socket accept exception", e);
            throw;
        }
    }

    private async Task Multiplex(SocketMap sockMap, Contextualizer ctx) {
        try {
            await Multiplexer.Multiplex(Repeatedly.Invoke(MakeWebSocket), sockMap, ctx);
        } catch (OperationCanceledException) {
            await Log.Write("cancelled multiplexing");
            throw;
        } catch (Exception e) {
            await Log.Warn("unexpected multiplexing exception", e);
            throw;
        }
    }

    private ClientWebSocket MakeWebSocket() {
        var ws = new ClientWebSocket();

        try {
            config.Proxy.Configure(ws, config.TunnelUri);
        } catch (Exception e) {
            throw new Exception("failed to configure ws proxy settings", e);
        }

        return ws;
    }
}