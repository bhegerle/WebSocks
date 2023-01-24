using WebStunnel;

if (args.Length == 0)
    throw new Exception("usage: websocks.exe {config path}");

var config = await Config.Load(args[0]);
if (config.LogPath != null)
    Utils.SetLogPath(config.LogPath);

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, evt) => {
    cts.Cancel();
    Thread.Sleep(1000);
};

if (config.ListenUri.Scheme == "tcp") {
    var cli = new TcpServer(config);
    await cli.Start(cts.Token);
} else {
    var srv = new WebSocketsServer(config);
    await srv.Start();
}