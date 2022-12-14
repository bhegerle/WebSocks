using System.Net.WebSockets;

namespace websocks;

internal class WebSocketReceiver
{
    private const int BufferLen = 1024 * (1024 + 1);
    private readonly byte[] _buffer;
    private readonly CancellationTokenSource _cts;

    private readonly WebSocket _webSocket;

    internal WebSocketReceiver(WebSocket webSocket)
    {
        _webSocket = webSocket;

        _buffer = new byte[BufferLen];
        _cts = new CancellationTokenSource();
    }

    internal async Task Transit()
    {
        Console.WriteLine("receiving on websocket");

        while (_webSocket.State == WebSocketState.Open)
        {
            var msg = await ReadMsg();
            Console.WriteLine($"received {msg.Count}");
        }
    }

    private async Task<ArraySegment<byte>> ReadMsg()
    {
        var seg = new ArraySegment<byte>(_buffer);

        var offset = 0;
        while (offset < seg.Count)
        {
            var recv = await _webSocket.ReceiveAsync(seg[offset..], _cts.Token);
            offset += recv.Count;

            if (recv.EndOfMessage)
                return seg[..offset];
        }

        throw new Exception("message exceeds segment");
    }
}