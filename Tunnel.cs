namespace WebStunnel;

internal sealed class Tunnel : IDisposable {
    private readonly ProtocolByte _protoByte;
    private readonly Config _config;
    private readonly IWebSocketSource _wsEnum;
    private readonly SemaphoreSlim _mutex;
    private Channel _curr;

    internal Tunnel(ProtocolByte protoByte, Config config, IWebSocketSource wsSrc) {
        _protoByte = protoByte;
        _config = config;
        _wsEnum = wsSrc;
        _mutex = new SemaphoreSlim(1);
    }

    public void Dispose() {
        using (_curr)
        using (_mutex)
        using (_wsEnum)
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
        await _mutex.WaitAsync(token);
        try {
            if (_curr == null) {
                var ws = await _wsEnum.GetWebSocket(token);
                var c = new Codec(_protoByte, _config);
                var next = new Channel(ws, c);

                await next.HandshakeCheck(token);

                _curr = next;
            }

            return _curr;
        } finally {
            _mutex.Release();
        }
    }

    private async Task Close(IDisposable d) {
        await _mutex.WaitAsync();
        try {
            d.Dispose();
            if (ReferenceEquals(_curr, d)) {
                _curr = null;
            }
        } finally {
            _mutex.Release();
        }
    }
}