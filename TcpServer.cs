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

        var ctx = new Contextualizer(ProtocolByte.TcpListener, config, token);
        var sockMap2 = new SocketMap(ctx, CannotConstructSocket);

        var at = AcceptLoop(sockMap2, ctx);
        var tt = Multiplex(sockMap2, ctx);
        await Task.WhenAll(at, tt);
    }

    public ValueTask DisposeAsync() {
        return ValueTask.CompletedTask;
    }

    private async Task AcceptLoop(SocketMap sockMap, Contextualizer ctx) {
        try {
            using var listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(config.ListenUri.EndPoint());
            listener.Listen();

            await Log.Write($"listening on {listener.LocalEndPoint}");

            while (true) {
                using var ln=ctx.Link();
                var s = await listener.AcceptAsync(ln.Token);

                var sctx = ctx.Contextualize(new SocketId(), s);

                await Log.Write($"accepted connection {sctx.Id} from {s.RemoteEndPoint}");

                await sockMap.Add(sctx);
            }
        } catch (OperationCanceledException) {
            await Log.Write("cancelled socket accept");
            throw;
        } catch (Exception e) {
            await Log.Warn("unexpected socket accept exception", e);
            throw;
        }
    }

    private async Task Multiplex(SocketMap sockMap, Contextualizer ctx) {
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

    private static Socket CannotConstructSocket() {
        throw new Exception("cannot construct socket");
    }
}