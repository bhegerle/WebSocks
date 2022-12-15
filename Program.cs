using WebStunnel;

if (args.Length == 0)
    throw new Exception("usage: websocks.exe {config path}");

var config = await Config.Load(args[0]);

if (config.ListenUri.Scheme == "tcp")
{
    var cli = new TcpServer(config);
    await cli.Start();
}
else
{
    var srv = new WebSocketsServer(config);
    await srv.Start();
}