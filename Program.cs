using WebSocks;

if (args.Length == 0)
    throw new Exception("usage: websocks.exe {config path}");

var config = await Config.Load(args[0]);

var codec = new Codec(config.Key);
var listenUri = new Uri(config.ListenOn);

if (listenUri.Scheme == "socks4")
{
    var cli = new Client(listenUri, new Uri(config.TunnelTo));
    await cli.Start();
}
else
{
    var srv = new Server(listenUri);
    await srv.Start();
}