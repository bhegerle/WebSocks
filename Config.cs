using System.Text.Json;
using System.Text.Json.Serialization;

namespace shock;

public record Config
{
    public Mode Mode { get; init; }
    public string Address { get; init; }
    public string Key { get; init; }

    internal static async Task<Config> Load(string path)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read);

        var opt = new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        var conf = await JsonSerializer.DeserializeAsync<Config>(file, opt);
        if (conf == null)
            throw new Exception("config cannot be null");

        return conf;
    }
}

public enum Mode
{
    Server,
    Client
}