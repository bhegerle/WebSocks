using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using static System.BitConverter;
using static System.Net.IPAddress;

namespace WebSocks;

internal static class Socks4Connector
{
    private const int Version = 4;
    private const int ConnectRequest = 1;
    private const int ConnectResponse = 0;
    private const int ConnectGranted = 90;

    internal static async Task<Socket> Connect(WebSocket webSocket, CancellationToken token)
    {
        var buffer = new byte[9];
        var res = await webSocket.ReceiveAsync(buffer, token);

        if (!res.EndOfMessage)
            throw new Exception("incomplete complete connect message");

        var vn = buffer[0];
        var cmd = buffer[1];
        var port = NetworkToHostOrder(ToInt16(buffer, 2));
        var addr = new IPAddress(buffer[4..8]);
        var nul = buffer[8];

        if (vn != Version)
            throw new Exception("unsupported SOCKS version number");

        if (cmd != ConnectRequest)
            throw new Exception("expected SOCKS connect message");

        if (nul != 0)
            throw new Exception("expected SOCKS connect message terminal null byte");

        var ep = new IPEndPoint(addr, port);

        Console.WriteLine($"received connect request for {ep}");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(ep, token);

        Console.WriteLine($"connected to {ep}");

        buffer = buffer[..8];
        buffer[0] = ConnectResponse;
        buffer[1] = ConnectGranted;

        await webSocket.SendAsync(buffer,
            WebSocketMessageType.Binary,
            WebSocketMessageFlags.EndOfMessage | WebSocketMessageFlags.DisableCompression,
            token);
        Console.WriteLine("connect request granted");

        return socket;
    }
}