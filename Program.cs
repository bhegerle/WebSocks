using shock;

if (args.Length == 0)
    throw new Exception("usage: shock.exe {config path}");

var config = await Config.Load(args[0]);

var codec = new Codec(config.Key);

if (config.Mode == Mode.Server)
{
    var srv = new Server(config.Address);
    await srv.Start();
}
else
{
    var cli = new Client(config.Address);
    await cli.Start();
}