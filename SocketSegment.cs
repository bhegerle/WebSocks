namespace WebStunnel;

internal struct SocketSegment {
    internal SocketSegment(SocketId id, ArraySegment<byte> seg) {
        Seg = seg;
        Id = id;
    }

    internal ArraySegment<byte> Seg { get; private set; }
    internal SocketId Id { get; }
    internal int Count => Seg.Count;

    internal void Resize(int n) {
        Seg = Seg[..n];
    }
}
