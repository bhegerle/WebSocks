namespace WebStunnel;

internal class ChannelConnector {
    private readonly ProtocolByte protoByte;
    private readonly Config config;
    private readonly IWebSocketSource wsSrc;

    internal ChannelConnector(ProtocolByte protoByte, Config config, IWebSocketSource wsSrc) {
        this.protoByte = protoByte;
        this.config = config;
        this.wsSrc = wsSrc;
    }

    public async Task<Channel> Connect(CancellationToken token) {
        var ws = await wsSrc.GetWebSocket(token);
        if (ws == null)
            return null;

        var c = new Channel(ws, new Codec(protoByte, config));
        try {
            await c.HandshakeCheck(token);
            return c;
        } catch {
            c.Dispose();
            throw;
        }
    }
}
