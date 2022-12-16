using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebStunnel;

internal class Bridge
{
    private readonly Codec _codec;
    private readonly Socket _sock;
    private readonly byte[] _sockRecvBuffer, _wsRecvBuffer;
    private readonly WebSocket _ws;

    internal Bridge(Socket sock, WebSocket ws, Config config)
    {
        _sock = sock;
        _ws = ws;
        _codec = new Codec(config);

        _sockRecvBuffer = new byte[1024 * 1024];
        _wsRecvBuffer = new byte[1024 * (1024 + 1)];
    }

    internal async Task Transit(CancellationToken token)
    {
        var s = SocketToWs(token);
        var ws = WsToSocket(token);

        await Task.WhenAny(s, ws);

        await Close(token);

        await s;
        await ws;
    }

    private async Task SocketToWs(CancellationToken token)
    {
        try
        {
            while (_sock.Connected)
            {
                var seg = Codec.GetAuthSegment(_sockRecvBuffer);
                var r = await _sock.ReceiveAsync(seg, SocketFlags.None, token);

                if (r == 0)
                    break;

                seg = _codec.AuthMessage(seg[..r]);

                await _ws.SendAsync(seg,
                    WebSocketMessageType.Binary,
                    WebSocketMessageFlags.EndOfMessage | WebSocketMessageFlags.DisableCompression,
                    token);

                Console.WriteLine($"sock->ws: {seg.Count} bytes");
            }
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
                var seg = _wsRecvBuffer.AsSegment();

                while (seg.Count > 0)
                {
                    var recv = await _ws.ReceiveAsync(seg, token);
                    seg = seg[recv.Count..];

                    if (recv.EndOfMessage)
                        break;

                    Console.WriteLine($"ws partial: buffered {recv.Count} bytes");
                }

                if (seg.Offset == 0)
                    break;

                if (seg.Count <= 0)
                    throw new Exception("message exceeds segment");

                seg = _codec.VerifyMessage(_wsRecvBuffer.AsSegment(0, seg.Offset));

                await _sock.SendAsync(seg, SocketFlags.None);

                Console.WriteLine($"ws->sock: {seg.Count} bytes");
            }
        } catch (Exception e)
        {
            Console.WriteLine($"ws->sock: exception {e.Message}");
        }
    }

    private async Task Close(CancellationToken token)
    {
        try
        {
            if (_sock.Connected)
                _sock.Close();
        } catch
        {
            // ignored
        }

        try
        {
            if (_ws.State != WebSocketState.Closed)
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, token);
        } catch
        {
            // ignored
        }
    }
}