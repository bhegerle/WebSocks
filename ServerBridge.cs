using System.Net.WebSockets;

namespace WebSocks;

internal class ServerBridge
{
    private readonly WebSocket _webSocket;
    private readonly Bridge _bridge;
    private readonly CancellationTokenSource _cts;
    private readonly DummyReceiver _dummyReceiver;

    internal ServerBridge(WebSocket webSocket)
    {
        _webSocket = webSocket;
        _bridge = new Bridge(Recv, Send);
        _cts = new CancellationTokenSource();
        _dummyReceiver = new DummyReceiver();
    }

    internal async Task Transit()
    {
        await _bridge.Transit();
    }

    private async Task Send(byte[] buffer)
    {
        await _webSocket.SendAsync(buffer ?? Array.Empty<byte>(),
            WebSocketMessageType.Binary,
            buffer == null,
            _cts.Token);
    }

    private async Task<byte[]> Recv()
    {
        return await _dummyReceiver.Receive();
    }
}