using WebStunnel;

if (args.Length == 0)
    throw new Exception("usage: websocks.exe {config path}");

var config = await Config.Load(args[0]);
await Log.Configure(config);

using var cts = new CancellationTokenSource();
Control.FromConsole(cts);

IServer server;
if (config.ListenUri.Scheme == "tcp")
    server = new TcpServer(config);
else
    server = new WebSocketsServer(config);

var srvTask = server.Start(cts.Token);

try {
    await srvTask;
} catch (OperationCanceledException) {
    // ignored
} catch (Exception ex) {
    await Log.Error("unhandled exception", ex);
}

await Log.Write("bye");
