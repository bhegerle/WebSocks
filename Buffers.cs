namespace WebStunnel;

internal static class Buffers {
    internal static ArraySegment<byte> New() {
        return new ArraySegment<byte>(new byte[1024 * 1024]);
    }

    internal static SocketSegment New(SocketId id) {
        var seg = New();
        seg = seg[..Message.Data.SuffixSize];
        return new SocketSegment(id, seg);
    }
}