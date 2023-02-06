using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebStunnel;

internal class TcpServer : IServer {
    private readonly Config config;
    private readonly SemaphoreSlim mutex;
    private Multiplexer mux;

    internal TcpServer(Config config) {
        config.ListenUri.CheckUri("listen", "tcp");
        config.TunnelUri.CheckUri("bridge", "ws");

        this.config = config;

        mutex = new SemaphoreSlim(1);
    }

    public async Task Start(CancellationToken token) {
        await Log.Write($"tunneling {config.ListenUri} -> {config.TunnelUri}");

        using var listener = await CreateListener();

        var ctx = new ServerContext(Side.TcpListener, config, token);

        var at = AcceptLoop(listener, ctx);
        var tt = Multiplex(ctx);

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

    private async Task AcceptLoop(Socket listener, ServerContext ctx) {
        try {
            while (true) {
                await Log.Write("awaiting");
                var s = await listener.AcceptAsync(ctx.Token);

                await mutex.WaitAsync(ctx.Token);
                try {
                    if (mux != null) {
                        await mux.Multiplex(s);
                    } else {
                        s.Dispose();
                        await Log.Warn("no active multiplexer");
                    }
                } finally {
                    mutex.Release();
                }
            }
        } catch (Exception e) {
            await Log.Warn("unexpected socket accept exception", e);
            throw;
        }
    }

    private async Task Multiplex(ServerContext ctx) {
        var wsSeq = WebSocketSequence().RateLimited(config.ReconnectDelay, ctx.Token);

        await foreach (var ws in wsSeq) {
            using var wsMux = new Multiplexer(ctx);

            await SetMux(wsMux);
            try {
                await wsMux.Multiplex(ws);
            } catch (OperationCanceledException) {
                await Log.Write("cancelled multiplexing");
            } catch (Exception e) {
                await Log.Warn("unexpected multiplexing exception", e);
            } finally {
                await SetMux(null);
            }
        }

        async Task SetMux(Multiplexer m) {
            await mutex.WaitAsync(ctx.Token);
            try {
                mux = m;
            } finally {
                mutex.Release();
            }
        }
    }

    private IEnumerable<ClientWebSocket> WebSocketSequence() {
        while (true) {
            var ws = new ClientWebSocket();

            try {
                config.Proxy.Configure(ws, config.TunnelUri);
            } catch (Exception e) {
                throw new Exception("failed to configure ws proxy settings", e);
            }

            yield return ws;
        }
    }
}