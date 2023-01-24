namespace WebStunnel;

internal sealed class Tunnel : IDisposable {
    private readonly ProtocolByte protoByte;
    private readonly Config config;
    private readonly IWebSocketSource wsSrc;
    private readonly SemaphoreSlim mutex;
    private Channel curr;

    internal Tunnel(ProtocolByte protoByte, Config config, IWebSocketSource wsSrc) {
        this.protoByte = protoByte;
        this.config = config;
        this.wsSrc = wsSrc;
        mutex = new SemaphoreSlim(1);
    }

    public void Dispose() {
        curr?.Dispose();
        //wsSrc.Dispose();
        mutex.Dispose();
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
                var ws = await wsSrc.GetWebSocket(token);
                if (ws == null)
                    throw new NullReferenceException(nameof(ws));

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
            Console.WriteLine("C");
            d.Dispose();
            if (ReferenceEquals(curr, d)) {
                curr = null;
            }
        } finally {
            mutex.Release();
        }
    }
}