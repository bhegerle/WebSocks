namespace WebStunnel;

internal sealed class Tunnel : IDisposable {
    private readonly ProtocolByte protoByte;
    private readonly Config config;
    private readonly IWebSocketSource wsEnum;
    private readonly SemaphoreSlim mutex;
    private Channel curr;

    internal Tunnel(ProtocolByte protoByte, Config config, IWebSocketSource wsSrc) {
        this.protoByte = protoByte;
        this.config = config;
        wsEnum = wsSrc;
        mutex = new SemaphoreSlim(1);
    }

    public void Dispose() {
        using (curr)
        using (mutex)
        using (wsEnum)
            return;
    }

    internal async Task Send(ArraySegment<byte> seg, CancellationToken token) {
        var c = await GetCurrentChannel(token);
        try {
            await c.Send(seg, token);
        } catch {
            await Close(c);
            throw;
        }
    }

    internal async Task<ArraySegment<byte>> Receive(ArraySegment<byte> seg, CancellationToken token) {
        var c = await GetCurrentChannel(token);
        try {
            return await c.Receive(seg, token);
        } catch {
            await Close(c);
            throw;
        }
    }

    private async Task<Channel> GetCurrentChannel(CancellationToken token) {
        await mutex.WaitAsync(token);
        try {
            if (curr == null) {
                var ws = await wsEnum.GetWebSocket(token);
                var c = new Codec(protoByte, config);
                var next = new Channel(ws, c);

                await next.HandshakeCheck(token);

                curr = next;
            }

            return curr;
        } finally {
            mutex.Release();
        }
    }

    private async Task Close(IDisposable d) {
        await mutex.WaitAsync();
        try {
            d.Dispose();
            if (ReferenceEquals(curr, d)) {
                curr = null;
            }
        } finally {
            mutex.Release();
        }
    }
}