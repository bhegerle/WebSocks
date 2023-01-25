namespace WebStunnel;

internal class ChannelConnectionException : Exception {
    internal ChannelConnectionException(Exception e) 
        : base("could not connect channel", e) { }
}

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
        Channel c = null;

        try {
            var ws = await wsSrc.GetWebSocket(token);
            if (ws == null)
                return null;

            c = new Channel(ws, new Codec(protoByte, config));
            await c.HandshakeCheck(token);
            return c;
        } catch (Exception e) {
            c?.Dispose();
            throw new ChannelConnectionException(e);
        }
    }
}
