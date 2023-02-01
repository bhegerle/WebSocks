namespace WebStunnel;

internal readonly struct Frame {
    private readonly int suffixSize;

    internal Frame(ArraySegment<byte> x, int suffixSize, bool extend) {
        this.suffixSize = suffixSize;

        if (extend)
            x = x.Extend(suffixSize);

        Complete = x;
    }

    internal ArraySegment<byte> Complete { get; }

    internal int FramedCount => Complete.Count;

    internal ArraySegment<byte> Message => Complete[..^suffixSize];
    internal ArraySegment<byte> Suffix => Complete[^suffixSize..];
}
