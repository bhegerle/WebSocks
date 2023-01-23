using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebStunnel;

internal class Bridge {
    private readonly Codec codec;
    private readonly Socket sock;
    private readonly byte[] sockRecvBuffer, wsRecvBuffer;
    private readonly WebSocket ws;

    internal Bridge(Socket sock, WebSocket ws, ProtocolByte protoByte, Config config) {
        this.sock = sock;
        this.ws = ws;
        codec = new Codec(protoByte, config);

        sockRecvBuffer = new byte[1024 * 1024];
        wsRecvBuffer = new byte[1024 * (1024 + 1)];
    }

    internal async Task Transit() {
        await Handshake();

        var cts = new CancellationTokenSource();

        var s = SocketToWs(cts.Token);
        var ws = WsToSocket(cts.Token);

        await Task.WhenAny(s, ws);

        await Close();

        cts.Cancel();

        await s;
        await ws;
    }

    private async Task Handshake() {
        var token = Utils.TimeoutToken();

        Console.WriteLine("init handshake");
        var init = sockRecvBuffer.AsSegment(0, Codec.InitMessageSize);
        init = codec.InitHandshake(init);

        Console.WriteLine("  sending");
        await WsSend(init, token);

        Console.WriteLine("  receiving");
        var (remInit, handle) = await WsRecv(token);
        if (!handle)
            throw new Exception("did not receive handshake");

        Console.WriteLine("  verifying");
        codec.VerifyHandshake(remInit);

        Console.WriteLine("  ok");
    }

    private async Task SocketToWs(CancellationToken token) {
        try {
            while (sock.Connected) {
                var (seg, handle) = await SockRecv(token);
                if (!handle)
                    break;

                seg = codec.AuthMessage(seg);

                await WsSend(seg, token);
            }

            Console.WriteLine("finished receiving on socket");
        } catch (Exception e) {
            Console.WriteLine($"sock->ws: exception {e.Message}");
        }
    }

    private async Task WsToSocket(CancellationToken token) {
        try {
            while (ws.State == WebSocketState.Open) {
                var (seg, handle) = await WsRecv(token);
                if (!handle)
                    continue;

                seg = codec.VerifyMessage(seg);

                await SockSend(seg, token);
            }

            Console.WriteLine("finished receiving on WebSocket");
        } catch (Exception e) {
            Console.WriteLine($"ws->sock: exception {e.Message}");
        }
    }

    private async Task<(ArraySegment<byte> seg, bool handle)> SockRecv(CancellationToken token) {
        Array.Fill(sockRecvBuffer, default);
        var seg = sockRecvBuffer.AsSegment();

        var r = await sock.ReceiveAsync(seg, SocketFlags.None, token);

        seg = seg[..r];
        var handle = r > 0;

        return (seg, handle);
    }

    private async Task SockSend(ArraySegment<byte> seg, CancellationToken token) {
        await sock.SendAsync(seg, SocketFlags.None, token);
    }

    private async Task<(ArraySegment<byte> seg, bool handle)> WsRecv(CancellationToken token) {
        Array.Fill(wsRecvBuffer, default);
        var seg = wsRecvBuffer.AsSegment();

        while (seg.Count > 0) {
            var recv = await ws.ReceiveAsync(seg, token);
            if (recv.MessageType != WebSocketMessageType.Binary)
                return (seg, false);

            seg = seg[recv.Count..];

            if (recv.EndOfMessage) {
                seg = seg.Array.AsSegment(0, seg.Offset);
                return (seg, true);
            }

            Console.WriteLine($"ws partial: buffered {recv.Count} bytes");
        }

        throw new Exception("message exceeds segment");
    }

    private async Task WsSend(ArraySegment<byte> seg, CancellationToken token) {
        await ws.SendAsync(seg,
            WebSocketMessageType.Binary,
            WebSocketMessageFlags.EndOfMessage | WebSocketMessageFlags.DisableCompression,
            token);
    }

    private async Task Close() {
        sock.ForceClose();
        await ws.ForceCloseAsync();
    }
}