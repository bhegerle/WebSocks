using System.Net.WebSockets;

namespace WebStunnel;

internal class ChannelException : Exception {
    internal ChannelException(string message, Exception e) : base(message, e) { }
}

internal class Channel : IDisposable {
    private readonly WebSocket ws;
    private readonly Codec codec;
    private readonly SemaphoreSlim mutex;

    internal Channel(WebSocket ws, Codec codec) {
        this.ws = ws;
        this.codec = codec;

        mutex = new SemaphoreSlim(1);
    }

    public void Dispose() {
        ws.Dispose();
    }

    internal async Task HandshakeCheck(CancellationToken token) {
        await mutex.WaitAsync(token);
        try {
            if (codec.State == CodecState.Init) {
                await Log.Write("    init handshake");

                var seg = new byte[Codec.InitMessageSize];
                var sendSeg = codec.InitHandshake(seg);
                await WsSend(sendSeg, token);

                var recvSeg = await WsRecv(seg, token);
                codec.VerifyHandshake(recvSeg);

                await Log.Write("    completed handshake");
            }
        } catch (Exception ex) {
            throw new ChannelException("handshake failed", ex);
        } finally {
            mutex.Release();
        }
    }

    internal async Task Send(ArraySegment<byte> seg, CancellationToken token) {
        await mutex.WaitAsync(token);
        try {
            seg = codec.AuthMessage(seg);
        } finally {
            mutex.Release();
        }

        await WsSend(seg, token);
    }

    internal async Task<ArraySegment<byte>> Receive(ArraySegment<byte> seg, CancellationToken token) {
        seg = await WsRecv(seg, token);

        await mutex.WaitAsync(token);
        try {
            seg = codec.VerifyMessage(seg);
        } finally {
            mutex.Release();
        }

        return seg;
    }

    private async Task<ArraySegment<byte>> WsRecv(ArraySegment<byte> seg, CancellationToken token) {
        var init = seg;
        var n = 0;

        while (seg.Count > 0) {
            var recv = await ws.ReceiveAsync(seg, token);
            if (recv.MessageType != WebSocketMessageType.Binary)
                throw new Exception("non-binary message received");

            n += recv.Count;
            seg = seg[recv.Count..];

            if (recv.EndOfMessage) {
                return init[..n];
            }

            await Log.Write($"ws partial: buffered {recv.Count} bytes");
        }

        throw new Exception("message exceeds segment");
    }

    private async Task WsSend(ArraySegment<byte> seg, CancellationToken token) {
        await ws.SendAsync(seg,
            WebSocketMessageType.Binary,
            WebSocketMessageFlags.EndOfMessage | WebSocketMessageFlags.DisableCompression,
            token);
    }
}
