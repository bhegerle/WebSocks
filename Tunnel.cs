using System.Net.WebSockets;

namespace WebStunnel;

internal sealed class Tunnel : IDisposable {
    private readonly ProtocolByte _protoByte;
    private readonly Config _config;
    private readonly IWebSocketSource _wsEnum;
    private readonly SemaphoreSlim _mutex;
    private WebSocketEncoder _curr;

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
        var c = await GetCurrentWs(token);
        try {
            await c.Send(seg, token);
        } catch {
            await CloseWs(c);
            throw;
        }
    }

    internal async Task<ArraySegment<byte>> Receive(ArraySegment<byte> seg, CancellationToken token) {
        var c = await GetCurrentWs(token);
        try {
            return await c.Receive(seg, token);
        } catch {
            await CloseWs(c);
            throw;
        }
    }

    private async Task<WebSocketEncoder> GetCurrentWs(CancellationToken token) {
        await _mutex.WaitAsync(token);
        try {
            if (_curr == null) {
                var w = await _wsEnum.GetWebSocket(token);
                var c = new Codec(_protoByte, _config);

                var nextWs = new WebSocketEncoder(w, c);
                await nextWs.HandshakeCheck(token);

                _curr = nextWs;
            }

            return _curr;
        } finally {
            _mutex.Release();
        }
    }

    private async Task CloseWs(IDisposable d) {
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