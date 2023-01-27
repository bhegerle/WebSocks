using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace WebStunnel;

internal class ChannelConnectionException : Exception {
    internal ChannelConnectionException(Exception e)
        : base("could not connect channel", e) { }
}

internal class ChannelConnector2 {
    private readonly ProtocolByte protoByte;
    private readonly Config config;

    internal ChannelConnector2(ProtocolByte protoByte, Config config) {
        this.protoByte = protoByte;
        this.config = config;
    }

    internal async IAsyncEnumerable<Channel> Connect(IAsyncEnumerable<WebSocket> webSocketSeq,
        [EnumeratorCancellation] CancellationToken token = default) {
        await foreach (var ws in webSocketSeq.WithCancellation(token)) {
            var codec = new Codec(protoByte, config);
            var c = new Channel(ws, codec);

            try {
                await c.HandshakeCheck(token);
            } catch {
                continue;
            }

            yield return c;
        }
    }
}

internal class ChannelConnector {
    private readonly ProtocolByte protoByte;
    private readonly Config config;
    private readonly IWebSocketSource wsSrc;
    private readonly IAsyncEnumerable<WebSocket> wsSeq;
    private int attempt;
    private IAsyncEnumerator<WebSocket> wsEnum;

    internal ChannelConnector(ProtocolByte protoByte, Config config, IWebSocketSource wsSrc,
        IAsyncEnumerable<WebSocket> wsSeq) {
        this.protoByte = protoByte;
        this.config = config;
        this.wsSrc = wsSrc;
        this.wsSeq = wsSeq;
    }

    internal async Task<Channel> Connect(CancellationToken token) {



        Console.WriteLine("a" + token.WaitHandle.Handle);

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
