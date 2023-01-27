﻿using System.Net.WebSockets;

namespace WebStunnel;

internal class WebSocketContext : IDisposable {
    private readonly WebSocket ws;
    private readonly SemaphoreSlim mutex;
    private readonly Codec codec;
    private readonly SocketCancellation cancellation;
    private Connector connector;

    internal WebSocketContext(WebSocket ws, Codec codec, SocketCancellation cancellation) {
        this.ws = ws;
        this.codec = codec;
        this.cancellation = cancellation;

        mutex = new SemaphoreSlim(1);
    }

    internal WebSocketContext(ClientWebSocket ws, Uri connectTo, Codec codec, SocketCancellation cancellation)
        : this(ws, codec, cancellation) {
        connector = new Connector(ws, connectTo);
    }

    internal async Task Send(ArraySegment<byte> seg) {
        using var sendTimeout = cancellation.IdleTimeout();
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
        using var recvTimeout = cancellation.IdleTimeout();
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
                await WebSock.ConnectAsync(ConnectTo, token);
            } catch (Exception ex) {
                throw new Exception("websocket connect failed", ex);
                throw;
            }
        }
    }
}