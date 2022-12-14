using shock;

if (args.Length == 0)
    throw new Exception("usage: shock.exe {config path}");

var config = await Config.Load(args[0]);

var buffer = new byte[3];
var stream = new MemoryStream();
var c = new Codec(config.Key);
c.Encode(buffer, stream);

stream.Seek(0, SeekOrigin.Begin);

var p = c.Decode(stream);


Console.WriteLine();