using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebStunnel;

public record Config {
    public string ListenOn { get; init; }
    public string TunnelTo { get; init; }
    public ProxyConfig Proxy { get; init; } = new() { UseSystemProxy = true };
    public string Key { get; init; }
    public string LogPath { get; init; }
    public bool Verbose { get; init; }

    internal Uri ListenUri => new(ListenOn);
    internal Uri TunnelUri => new(TunnelTo);

    internal static TimeSpan Timeout => TimeSpan.FromSeconds(20);
    internal static TimeSpan IdleTimeout => TimeSpan.FromMinutes(1);
    internal static TimeSpan ReconnectTimeout => TimeSpan.FromSeconds(7);
    internal static TimeSpan ConnectTimeout => TimeSpan.FromSeconds(2);

    internal static async Task<Config> Load(string path) {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read);

        var opt = new JsonSerializerOptions {
            Converters = {
                new JsonStringEnumConverter()
            }
        };

        var conf = await JsonSerializer.DeserializeAsync<Config>(file, opt);
        if (conf == null)
            throw new Exception("config cannot be null");

        return conf;
    }
}
