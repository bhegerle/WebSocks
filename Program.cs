using WebStunnel;

if (args.Length == 0)
    throw new Exception("usage: websocks.exe {config path}");

var config = await Config.Load(args[0]);

var listenUri = new Uri(config.ListenOn);
Console.WriteLine(config.Proxy.HttpProxy);
if (config.ListenUri.Scheme == "socks4")
{
    var cli = new Socks4Server(config);
    await cli.Start();
}
else
{
    var srv = new WebSocketsServer(config);
    await srv.Start();
}