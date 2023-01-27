using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebStunnel;

public record Config {
    public Uri ListenUri { get; init; }
    public Uri TunnelUri { get; init; }
    public ProxyConfig Proxy { get; init; } = new() { UseSystemProxy = true };
    public string Key { get; init; }
    public string LogPath { get; init; }
    public bool Verbose { get; init; }

    public TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(10);

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
