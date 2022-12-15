using System.Net.Sockets;
using System.Net.WebSockets;

namespace WebSocks;

internal class Bridge
{
    private readonly Socket _sock;
    private readonly byte[] _sockRecvBuffer, _wsRecvBuffer;
    private readonly WebSocket _ws;

    internal Bridge(Socket sock, WebSocket ws)
    {
        _sock = sock;
        _ws = ws;

        _sockRecvBuffer = new byte[1024 * 1024];
        _wsRecvBuffer = new byte[1024 * (1024 + 1)];
    }

    internal async Task Transit(CancellationToken token)
    {
        var s = SocketToWs(token);
        var ws = WsToSocket(token);

        await s;
        await ws;
    }

    private async Task SocketToWs(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var seg = new ArraySegment<byte>(_sockRecvBuffer);
            var r = await _sock.ReceiveAsync(seg, SocketFlags.None, token);
            seg = seg[..r];

            await _ws.SendAsync(seg, WebSocketMessageType.Binary, true, token);
        }
    }

    private async Task WsToSocket(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var seg = new ArraySegment<byte>(_wsRecvBuffer);

            while (seg.Count > 0)
            {
                var recv = await _ws.ReceiveAsync(seg, token);
                seg = seg[recv.Count..];

                if (recv.EndOfMessage)
                    break;
            }

            if (seg.Count <= 0)
                throw new Exception("message exceeds segment");

            seg = new ArraySegment<byte>(_wsRecvBuffer, 0, seg.Offset);

            await _sock.SendAsync(seg, SocketFlags.None);
        }
    }
}