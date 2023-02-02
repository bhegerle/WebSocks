using System.Net.WebSockets;

namespace WebStunnel;

internal class WebSocketContext : IDisposable {
    private readonly WebSocket ws;
    private readonly SemaphoreSlim mutex;
    private readonly Protocol codec;
    private readonly SocketTiming cancellation;
    private Connector connector;

    internal WebSocketContext(WebSocket ws, Protocol codec, SocketTiming cancellation) {
        this.ws = ws;
        this.codec = codec;
        this.cancellation = cancellation;

        mutex = new SemaphoreSlim(1);
    }

    internal WebSocketContext(ClientWebSocket ws, Uri connectTo, Protocol codec, SocketTiming cancellation)
        : this(ws, codec, cancellation) {
        connector = new Connector(ws, connectTo);
    }

    internal async Task Send(ArraySegment<byte> seg, SocketId id) {
        using var sendTimeout = cancellation.IdleTimeout();
        await Check(sendTimeout.Token);

        try {
            seg = codec.AuthMessage(seg, id.Value);
            await WsSend(seg, sendTimeout.Token);
            await Log.Trace($"ws\tsend {seg.Count} (w/suffix)");
        } catch (Exception e) {
            await Log.Warn("websocket send exception", e);
            await Cancel();
            throw;
        }
    }

    internal async Task<(ArraySegment<byte>, SocketId)> Receive(ArraySegment<byte> seg) {
        using var recvTimeout = cancellation.IdleTimeout();
        await Check(recvTimeout.Token);

        try {
            seg = await WsRecv(seg, recvTimeout.Token);
            await Log.Trace($"ws\trecv {seg.Count} (w/suffix)");
            var (payload, id) = codec.VerifyMessage(seg);
            return (payload, new SocketId(id));
        } catch (Exception e) {
            await Log.Warn("websocket receive exception", e);
            await Cancel();
            throw;
        }
    }

    private async Task Cancel() {
        await Log.Warn($"websocket cancelled");
        cancellation.Cancel();
    }

    public void Dispose() {
        ws.Dispose();
        cancellation.Dispose();
        mutex.Dispose();
    }

    private async Task Check(CancellationToken token) {
        await mutex.WaitAsync(token);
        try {
            if (codec.State == CodecState.Active)
                return;

            using var conTimeout = cancellation.ConnectTimeout(token);
            if (connector != null) {
                await connector.Connect(conTimeout.Token);
                connector = null;
            }

            if (codec.State == CodecState.Init) {
                await HandshakeCheck(conTimeout.Token);
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

            var sendSeg = codec.InitHandshake();
            await WsSend(sendSeg, token);

            var seg = new byte[Protocol.InitMessageSize];
            var recvSeg = await WsRecv(seg, token);
            codec.VerifyHandshake(recvSeg);

            await Log.Write("    completed handshake");
        } catch (Exception ex) {
            throw new Exception("handshake failed", ex);
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
                await WebSock.ConnectAsync(ConnectTo, token);
            } catch (Exception ex) {
                throw new Exception("websocket connect failed", ex);
                throw;
            }
        }
    }
}