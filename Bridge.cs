using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebStunnel;

internal class Bridge
{
    private readonly Codec _codec;
    private readonly Socket _sock;
    private readonly byte[] _sockRecvBuffer, _wsRecvBuffer;
    private readonly WebSocket _ws;

    internal Bridge(Socket sock, WebSocket ws, ProtocolByte protoByte, Config config)
    {
        _sock = sock;
        _ws = ws;
        _codec = new Codec(protoByte, config);

        _sockRecvBuffer = new byte[1024 * 1024];
        _wsRecvBuffer = new byte[1024 * (1024 + 1)];
    }

    internal async Task Transit()
    {
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

    private async Task Handshake()
    {
        var token = Utils.TimeoutToken();

        var init = _sockRecvBuffer.AsSegment(0, Codec.InitMessageSize);
        init = _codec.InitHandshake(init);

        await WsSend(init, token);

        var (remInit, handle) = await WsRecv(token);
        if (!handle)
            throw new Exception("did not receive handshake");

        _codec.VerifyHandshake(remInit);
    }

    private async Task SocketToWs(CancellationToken token)
    {
        try
        {
            while (_sock.Connected)
            {
                var (seg, handle) = await SockRecv(token);
                if (!handle)
                    break;

                seg = _codec.AuthMessage(seg);

                await WsSend(seg, token);

                Console.WriteLine($"sock->ws: {seg.Count} bytes");
            }

            Console.WriteLine("finished receiving on socket");
        } catch (Exception e)
        {
            Console.WriteLine($"sock->ws: exception {e.Message}");
        }
    }

    private async Task WsToSocket(CancellationToken token)
    {
        try
        {
            while (_ws.State == WebSocketState.Open)
            {
                var (seg, handle) = await WsRecv(token);
                if (!handle)
                    continue;

                seg = _codec.VerifyMessage(seg);

                await SockSend(seg, token);

                Console.WriteLine($"ws->sock: {seg.Count} bytes");
            }

            Console.WriteLine("finished receiving on WebSocket");
        } catch (Exception e)
        {
            Console.WriteLine($"ws->sock: exception {e.Message}");
        }
    }

    private async Task<(ArraySegment<byte> seg, bool handle)> SockRecv(CancellationToken token)
    {
        Array.Fill(_sockRecvBuffer, default);
        var seg = _sockRecvBuffer.AsSegment();

        var r = await _sock.ReceiveAsync(seg, SocketFlags.None, token);

        seg = seg[..r];
        var handle = r > 0;

        return (seg, handle);
    }

    private async Task SockSend(ArraySegment<byte> seg, CancellationToken token)
    {
        await _sock.SendAsync(seg, SocketFlags.None, token);
    }

    private async Task<(ArraySegment<byte> seg, bool handle)> WsRecv(CancellationToken token)
    {
        Array.Fill(_wsRecvBuffer, default);
        var seg = _wsRecvBuffer.AsSegment();

        while (seg.Count > 0)
        {
            var recv = await _ws.ReceiveAsync(seg, token);
            if (recv.MessageType != WebSocketMessageType.Binary)
                return (seg, false);

            seg = seg[recv.Count..];

            if (recv.EndOfMessage)
            {
                seg = seg.Array.AsSegment(0, seg.Offset);
                return (seg, true);
            }

            Console.WriteLine($"ws partial: buffered {recv.Count} bytes");
        }

        throw new Exception("message exceeds segment");
    }

    private async Task WsSend(ArraySegment<byte> seg, CancellationToken token)
    {
        await _ws.SendAsync(seg,
            WebSocketMessageType.Binary,
            WebSocketMessageFlags.EndOfMessage | WebSocketMessageFlags.DisableCompression,
            token);
    }

    private async Task Close()
    {
        _sock.ForceClose();
        await _ws.ForceCloseAsync();
    }
}