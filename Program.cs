using shock;

if (args.Length == 0)
    throw new Exception("usage: shock.exe {config path}");

var config = await Config.Load(args[0]);
new Codec(config.Key);
Console.WriteLine();