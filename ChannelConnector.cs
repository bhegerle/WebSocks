using System.Net.WebSockets;
using System.Threading.Channels;

namespace WebStunnel;

internal class ChannelConnectionException : Exception {
    internal ChannelConnectionException(Exception e)
        : base("could not connect channel", e) { }
}

internal class ChannelConnector {
    private readonly ProtocolByte protoByte;
    private readonly Config config;
    private readonly IWebSocketSource wsSrc;
    private int attempt;

    internal ChannelConnector(ProtocolByte protoByte, Config config, IWebSocketSource wsSrc) {
        this.protoByte = protoByte;
        this.config = config;
        this.wsSrc = wsSrc;
    }

    internal async Task<Channel> Connect(CancellationToken token) {
        attempt++;
        if (attempt > 1)
            await Task.Delay(config.ReconnectDelay, token);

        WebSocket ws = null;
        Channel c = null;

        try {
            ws = await wsSrc.GetWebSocket(token);
            if (ws == null)
                return null;

            c = new Channel(ws, new Codec(protoByte, config));

            await c.HandshakeCheck(token);
            return c;
        } catch (Exception e) {
            ws?.Dispose();
            c?.Dispose();
            throw new ChannelConnectionException(e);
        }
    }
}
