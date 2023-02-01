namespace WebStunnel;

internal static class Message {
    internal sealed class Init {
        internal Init(ArraySegment<byte> buffer) {
            Tagged = new TaggedPlaintext(buffer);
            SaltPair = new SaltPair(Tagged.Content);
        }

        internal const int MessageSize = SaltPair.MessageSize + TaggedPlaintext.SuffixSize;

        internal TaggedPlaintext Tagged { get; }
        internal SaltPair SaltPair { get; }
    }

    internal sealed class SaltPair {
        internal const int SaltSize = 32;
        internal const int MessageSize = 2 * SaltSize;

        internal SaltPair(ArraySegment<byte> buffer) {
            Buffer = buffer;
            (ReaderSalt, WriterSalt) = Split(buffer, SaltSize, SaltSize);
        }

        internal ArraySegment<byte> Buffer { get; }
        internal ArraySegment<byte> ReaderSalt { get; }
        internal ArraySegment<byte> WriterSalt { get; }
    }

    internal sealed class TaggedPlaintext {
        internal const int SaltSize = 32;
        internal const int TagSize = 16;
        internal const int SuffixSize = SaltSize + TagSize;

        internal TaggedPlaintext(ArraySegment<byte> buffer) {
            Buffer = buffer;
            (Content, Salt, Tag) = SplitSuffixes(buffer, SaltSize, TagSize);
        }

        internal ArraySegment<byte> Buffer { get; }
        internal ArraySegment<byte> Content { get; }
        internal ArraySegment<byte> Salt { get; }
        internal ArraySegment<byte> Tag { get; }
    }

    private static (ArraySegment<byte>, ArraySegment<byte>) Split(ArraySegment<byte> b, int n0, int n1) {
        var m = n0 + n1;
        if (b.Count != m)
            throw new Exception("wrong buffer size");
        return (b[..n0], b[n0..m]);
    }

    private static (ArraySegment<byte>, ArraySegment<byte>, ArraySegment<byte>) SplitSuffixes(ArraySegment<byte> b, int n1, int n2) {
        return Split(b, b.Count - n1 - n2, n1, n2);
    }

    private static (ArraySegment<byte>, ArraySegment<byte>, ArraySegment<byte>) Split(ArraySegment<byte> b, int n0, int n1, int n2) {
        var m0 = n0 + n1;
        var m1 = m0 + n2;
        if (b.Count != m1)
            throw new Exception("wrong buffer size");
        return (b[..n0], b[n0..m0], b[m0..m1]);
    }
}
