using System.Net;
using System.Net.Sockets;

namespace WebStunnel;

using static Timeouts;

internal sealed class SocketResolver {
    private readonly IPEndPoint endPoint;

    internal SocketResolver(Config config) {
        endPoint = config.TunnelUri.EndPoint();
    }

    internal async Task<Socket> Connect(CancellationToken token) {

        var s = new Socket(SocketType.Stream, ProtocolType.Tcp);

        try {
            using var conTimeout = ConnectTimeout(token);
            await s.ConnectAsync(endPoint, conTimeout.Token);
        } catch (Exception e) {
            await Log.Warn($"could not connect to {endPoint}", e);
            throw;
        }

        await Log.Write($"connected new socket to {endPoint}");
        throw new Exception();

        //return new SocketContext(s, id, token);
    }

    internal static async Task<SocketContext> Throw(SocketId id, CancellationToken _) {
        await Log.Warn($"rejecting request for socket {id}");
        throw new Exception("cannot resolve socket");
    }
}
