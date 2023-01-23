using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebStunnel;

internal class TcpServer {
    private readonly Config _config;
    private readonly List<Task> _newTasks;
    private readonly SemaphoreSlim _sem;
    private readonly ArraySegment<byte> _wsRecvBuffer;

    internal TcpServer(Config config) {
        config.ListenUri.CheckUri("listen", "tcp");
        config.TunnelUri.CheckUri("bridge", "ws");
        _config = config;

        _newTasks = new List<Task>();
        _sem = new SemaphoreSlim(1);

        _wsRecvBuffer = new byte[1024 * 1024];
    }

    private EndPoint EndPoint => _config.ListenUri.EndPoint();
    private Uri TunnelUri => _config.TunnelUri;
    private ProxyConfig ProxyConfig => _config.Proxy;

    internal async Task Start() {
        var autoWsSrc=new AutoconnectWebSocketSource(TunnelUri, ProxyConfig);
        var tunnel = new Tunnel(ProtocolByte.TcpListener, _config, autoWsSrc);

        var x = await tunnel.Receive(_wsRecvBuffer, Utils.IdleTimeout());

        return;

        await Probe.WsHost(TunnelUri, ProxyConfig);


        using var listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(EndPoint);
        listener.Listen();

        await Probe.WsHost(TunnelUri, ProxyConfig);

        Console.WriteLine($"tunneling {_config.ListenUri} -> {TunnelUri}");

        await AddTask(Accept(listener));

        var activeTasks = new List<Task>();
        while (true) {
            activeTasks.AddRange(await GetTasks());
            if (activeTasks.Count == 0) {
                break;
            }

            var doneTask = await Task.WhenAny(activeTasks);
            await doneTask;
            activeTasks.Remove(doneTask);
        }
    }

    private async Task Accept(Socket listener) {
        try {
            var s = await listener.AcceptAsync();
            await AddTask(Handle(s));

            if (!listener.SafeHandle.IsClosed) {
                await AddTask(Accept(listener));
            }
        } catch (Exception e) {
            Console.WriteLine($"exception in listen loop: {e}");
        }
    }

    private async Task Handle(Socket s) {
        try {
            Console.WriteLine($"connection from {s.RemoteEndPoint}");

            using var ws = new ClientWebSocket();
            ProxyConfig.Configure(ws, TunnelUri);

            await ws.ConnectAsync(TunnelUri, Utils.TimeoutToken());
            Console.WriteLine($"bridging through {TunnelUri}");

            var b = new Bridge(s, ws, ProtocolByte.TcpListener, _config);
            await b.Transit();
        } catch (Exception e) {
            Console.WriteLine($"exception in handling loop: {e}");
        } finally {
            Console.WriteLine("done handling connection");
            s.Dispose();
        }
    }

    private async Task AddTask(Task t) {
        await _sem.WaitAsync();
        try {
            _newTasks.Add(t);
        } finally {
            _sem.Release();
        }
    }

    private async Task<Task[]> GetTasks() {
        await _sem.WaitAsync();
        try {
            var a = _newTasks.ToArray();
            _newTasks.Clear();
            return a;
        } finally {
            _sem.Release();
        }
    }
}