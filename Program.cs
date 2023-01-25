using WebStunnel;

if (args.Length == 0)
    throw new Exception("usage: websocks.exe {config path}");

var config = await Config.Load(args[0]);
await Log.Configure(config);

using var cts = new CancellationTokenSource();

Task ioTask;
if (config.ListenUri.Scheme == "tcp") {
    var cli = new TcpServer(config);
    ioTask = cli.Start(cts.Token);
} else {
    var srv = new WebSocketsServer(config);
    ioTask = srv.Start(cts.Token);
}

var conTask = Control.RunUntilCancelled(cts);

try {
    await Task.WhenAll(conTask, ioTask);
} catch (OperationCanceledException) {
    // ignored
} catch (Exception ex) {
    await Log.Error("unhandled exception", ex);
}

await Log.Write("bye");
