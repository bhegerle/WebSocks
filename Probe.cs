namespace WebStunnel;

internal static class Probe {
    internal static async Task WsHost(Uri uri, ProxyConfig proxyConfig) {
        var testUri = new UriBuilder {
            Scheme = "http",
            Host = uri.Host,
            Port = uri.Port
        }.Uri;

        Console.WriteLine($"testing connection to {testUri}");

        var hch = new HttpClientHandler();
        proxyConfig.Configure(hch, testUri);

        using var client = new HttpClient(hch);

        var res = await client.GetAsync(testUri);

        res.EnsureSuccessStatusCode();
    }
}