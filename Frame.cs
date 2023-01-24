namespace WebStunnel;

internal readonly struct Frame {
    internal Frame(ArraySegment<byte> x, int suffixSize, bool extend) {
        SuffixSize = suffixSize;

        if (extend)
            x = x.Extend(suffixSize);

        Complete = x;
    }

    internal int SuffixSize { get; }
    internal ArraySegment<byte> Complete { get; }

    internal ArraySegment<byte> Message => Complete[..^SuffixSize];
    internal ArraySegment<byte> Suffix => Complete[^SuffixSize..];
}
