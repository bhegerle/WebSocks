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

    internal async Task Transit()
    {
        var cts = new CancellationTokenSource();

        var s = SocketToWs(cts.Token);
        var ws = WsToSocket(cts.Token);

        await Task.WhenAny(s, ws);

        await Close();

        cts.Cancel();

        await s;
        await ws;
    }

    private async Task SocketToWs(CancellationToken token)
    {
        try
        {
            while (_sock.Connected)
            {
                Array.Fill(_sockRecvBuffer, default);
                var seg = Codec.GetAuthSegment(_sockRecvBuffer);

                var r = await _sock.ReceiveAsync(seg, SocketFlags.None, token);
                if (r == 0)
                    break;

                seg = seg[..r];
                seg = _codec.AuthMessage(seg);

                await _ws.SendAsync(seg,
                    WebSocketMessageType.Binary,
                    WebSocketMessageFlags.EndOfMessage | WebSocketMessageFlags.DisableCompression,
                    token);

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
                Array.Fill(_wsRecvBuffer, default);
                var seg = _wsRecvBuffer.AsSegment();

                var msgCompl = false;
                while (seg.Count > 0)
                {
                    var recv = await _ws.ReceiveAsync(seg, token);
                    if (recv.MessageType == WebSocketMessageType.Close)
                        break;

                    seg = seg[recv.Count..];

                    if (recv.EndOfMessage)
                    {
                        msgCompl = true;
                        break;
                    }

                    Console.WriteLine($"ws partial: buffered {recv.Count} bytes");
                }

                if (seg.Count <= 0)
                    throw new Exception("message exceeds segment");

                if (msgCompl)
                {
                    seg = _codec.VerifyMessage(_wsRecvBuffer.AsSegment(0, seg.Offset));

                    await _sock.SendAsync(seg, SocketFlags.None);

                    Console.WriteLine($"ws->sock: {seg.Count} bytes");
                }
            }

            Console.WriteLine("finished receiving on WebSocket");
        } catch (Exception e)
        {
            Console.WriteLine($"ws->sock: exception {e.Message}");
        }
    }

    private async Task Close()
    {
        _sock.ForceClose();
        await _ws.ForceCloseAsync();
    }
}