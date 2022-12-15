using WebSocks;

if (args.Length == 0)
    throw new Exception("usage: websocks.exe {config path}");

var config = await Config.Load(args[0]);

var codec = new Codec(config.Key);
var listenUri = new Uri(config.ListenOn);

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