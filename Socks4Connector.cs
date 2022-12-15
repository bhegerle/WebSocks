using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using static System.BitConverter;
using static System.Net.IPAddress;

namespace WebSocks;

internal static class Socks4Connector
{
    internal static async Task<Socket> Connect(WebSocket webSocket, CancellationToken token)
    {
        var buffer = new byte[9];
        var res = await webSocket.ReceiveAsync(buffer, token);

        if (!res.EndOfMessage)
            throw new Exception("incomplete complete connect message");

        var vn = buffer[0];
        var cmd = buffer[1];
        var port = NetworkToHostOrder(ToInt16(buffer, 2));
        var addr = NetworkToHostOrder(ToInt32(buffer, 4));
        var nul = buffer[8];

        if (vn != 4)
            throw new Exception("unsupported SOCKS version number");

        if (cmd != 1)
            throw new Exception("expected SOCKS connect message");

        if (nul != 0)
            throw new Exception("expected SOCKS connect message terminal null byte");

        var ep = new IPEndPoint(new IPAddress(addr), port);

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(ep, token);

        buffer = buffer[..8];
        buffer[1] = 90;

        await webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, token);

        return socket;
    }
}