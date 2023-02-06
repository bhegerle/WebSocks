using System.Net.WebSockets;

namespace WebStunnel;

internal class WebSocketContext : IDisposable {
    private readonly WebSocket ws;
    private readonly SemaphoreSlim initMutex, recvMutex, sendMutex;
    private readonly Protocol proto;
    private readonly SocketTiming sockTiming;
    private Connector connector;

    internal WebSocketContext(WebSocket ws, ServerContext ctx, SocketTiming sockTiming) {
        this.ws = ws;
        this.sockTiming = sockTiming;

        proto = ctx.MakeProtocol();

        initMutex = new SemaphoreSlim(1);
        recvMutex = new SemaphoreSlim(1);
        sendMutex = new SemaphoreSlim(1);
    }

    internal WebSocketContext(ClientWebSocket ws, ServerContext ctx, SocketTiming sockTiming)
        : this((WebSocket)ws, ctx, sockTiming) {
        connector = new Connector(ws, ctx.WebSocketUri);
    }

    internal async Task Send(SocketSegment seg) {
        using var sendTimeout = sockTiming.IdleTimeout();
        await Check(sendTimeout.Token);

        await sendMutex.WaitAsync(sendTimeout.Token);
        try {
            var a = proto.AuthMessage(seg.Seg, seg.Id.Value);
            await WsSend(a, sendTimeout.Token);
        } catch (Exception e) {
            await Log.Warn("ws\tsend exception", e);
            await Cancel();
            throw;
        } finally {
            sendMutex.Release();
        }

        await Log.Trace($"ws\tsend {seg.Count}");
    }

    internal async Task<SocketSegment> Receive() {
        using var recvTimeout = sockTiming.IdleTimeout();
        await Check(recvTimeout.Token);

        ArraySegment<byte> payload;
        uint id;

        await recvMutex.WaitAsync(recvTimeout.Token);
        try {
            var b = Buffers.New();
            b = await WsRecv(b, recvTimeout.Token);
            (payload, id) = proto.VerifyMessage(b);
        } catch (Exception e) {
            await Log.Warn("ws\trecv exception", e);
            await Cancel();
            throw;
        } finally {
            recvMutex.Release();
        }

        await Log.Trace($"ws\trecv {payload.Count}");
        return new SocketSegment(new SocketId(id), payload);
    }

    public void Dispose() {
        ws.Dispose();
        sockTiming.Dispose();
        initMutex.Dispose();
    }

    private async Task Cancel() {
        await Log.Warn($"websocket cancelled");
        sockTiming.Cancel();
    }

    private async Task Check(CancellationToken token) {
        await initMutex.WaitAsync(token);
        try {
            if (proto.State == CodecState.Active)
                return;

            using var conTimeout = sockTiming.ConnectTimeout(token);
            if (connector != null) {
                await connector.Connect(conTimeout.Token);
                connector = null;
            }

            if (proto.State == CodecState.Init) {
                await HandshakeCheck(conTimeout.Token);
            }
        } catch {
            await Cancel();
            throw;
        } finally {
            initMutex.Release();
        }
    }

    private async Task HandshakeCheck(CancellationToken token) {
        try {
            await Log.Write("    init handshake");

            var sendSeg = proto.InitHandshake();
            await WsSend(sendSeg, token);

            var seg = new byte[Protocol.InitMessageSize];
            var recvSeg = await WsRecv(seg, token);
            proto.VerifyHandshake(recvSeg);

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