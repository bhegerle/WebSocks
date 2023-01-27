using System.Net.WebSockets;

namespace WebStunnel;

using static Timeouts;

internal class WebSocketContext : IDisposable {
    private readonly WebSocket ws;
    private readonly CancellationTokenSource cts;
    private readonly SemaphoreSlim mutex;
    private readonly Codec codec;
    private Connector connector;

    internal WebSocketContext(WebSocket ws, Codec codec, CancellationToken crossContextToken) {
        this.ws = ws;
        this.codec = codec;

        cts = CancellationTokenSource.CreateLinkedTokenSource(crossContextToken);
        mutex = new SemaphoreSlim(1);
    }

    internal WebSocketContext(ClientWebSocket ws, Uri connectTo, Codec codec, CancellationToken crossContextToken)
        : this(ws, codec, crossContextToken) {
        connector = new Connector(ws, connectTo);
    }

    internal async Task Send(ArraySegment<byte> seg) {
        using var sendTimeout = IdleTimeout(cts.Token);
        await Check(sendTimeout.Token);

        try {
            seg = codec.AuthMessage(seg);
            await WsSend(seg, sendTimeout.Token);
        } catch (Exception e) {
            await Log.Warn("websocket send exception", e);
            await Cancel();
            throw;
        }
    }

    internal async Task<ArraySegment<byte>> Receive(ArraySegment<byte> seg) {
        using var recvTimeout = IdleTimeout(cts.Token);
        await Check(recvTimeout.Token);

        try {
            seg = await WsRecv(seg, recvTimeout.Token);
            seg = codec.VerifyMessage(seg);
        } catch (Exception e) {
            await Log.Warn("websocket receive exception", e);
            await Cancel();
            throw;
        }

        return seg;
    }

    internal async Task Cancel() {
        await Log.Warn($"websocket cancelled");
        cts.Cancel();
    }

    public void Dispose() {
        ws.Dispose();
        cts.Dispose();
        mutex.Dispose();
    }

    private async Task Check(CancellationToken token) {
        await mutex.WaitAsync(token);
        try {
            if (connector != null) {
                using var conTimeout = ConnectTimeout(token);
                await connector.Connect(token);
                connector = null;
            }

            if (codec.State == CodecState.Init) {
                await HandshakeCheck(token);
            }
        } catch {
            await Cancel();
            throw;
        } finally {
            mutex.Release();
        }
    }

    private async Task HandshakeCheck(CancellationToken token) {
        try {
            await Log.Write("    init handshake");

            var seg = new byte[Codec.InitMessageSize];
            var sendSeg = codec.InitHandshake(seg);
            await WsSend(sendSeg, token);

            var recvSeg = await WsRecv(seg, token);
            codec.VerifyHandshake(recvSeg);

            await Log.Write("    completed handshake");
        } catch (Exception ex) {
            throw new Exception("handshake failed", ex);
            throw;
        }
    }

    private async Task WsSend(ArraySegment<byte> seg, CancellationToken token) {
        await ws.SendAsync(seg,
            WebSocketMessageType.Binary,
            WebSocketMessageFlags.EndOfMessage | WebSocketMessageFlags.DisableCompression,
            token);
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

    private record Connector(ClientWebSocket WebSock, Uri ConnectTo) {
        internal async Task Connect(CancellationToken token) {
            try {
                await Log.Write($"connecting to {ConnectTo}");
                using var conTimeout = ConnectTimeout(token);
                await WebSock.ConnectAsync(ConnectTo, conTimeout.Token);
            } catch (Exception ex) {
                throw new Exception("websocket connect failed", ex);
                throw;
            }
        }
    }
}