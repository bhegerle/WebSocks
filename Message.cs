namespace WebStunnel;

internal static class Message {
    internal sealed class Init {
        internal const int SaltSize = 32;
        internal const int TagSize = 16;
        internal const int MessageSize = 3 * SaltSize + TagSize;

        internal Init(ArraySegment<byte> buffer) {
            (Plaintext, InitSalt, Tag) = SplitSuffixes(buffer, SaltSize, TagSize);
            (ReaderSalt, WriterSalt) = Split(Plaintext, SaltSize, SaltSize);
        }

        internal ArraySegment<byte> Plaintext { get; }
        internal ArraySegment<byte> InitSalt { get; }
        internal ArraySegment<byte> Tag { get; }

        internal ArraySegment<byte> ReaderSalt { get; }
        internal ArraySegment<byte> WriterSalt { get; }
    }

    internal sealed class Data {
        internal const int TagSize = 16;
        internal const int IdSize = sizeof(uint);
        internal const int SuffixSize = TagSize + IdSize;

        private readonly ArraySegment<byte> idSeg;

        internal Data(ArraySegment<byte> buffer) {
            (Text, Tag) = SplitSuffix(buffer, TagSize);
            (Payload, idSeg) = SplitSuffix(Text, IdSize);
        }

        internal ArraySegment<byte> Text { get; }
        internal ArraySegment<byte> Tag { get; }

        internal ArraySegment<byte> Payload { get; }

        internal uint Id {
            get => BitConverter.ToUInt32(idSeg);
            set => BitConverter.TryWriteBytes(idSeg, value);
        }
    }

    private static (ArraySegment<byte>, ArraySegment<byte>) Split(ArraySegment<byte> b, int n0, int n1) {
        var m = n0 + n1;
        if (b.Count != m)
            throw new Exception("wrong buffer size");
        return (b[..n0], b[n0..m]);
    }

    private static (ArraySegment<byte>, ArraySegment<byte>, ArraySegment<byte>) Split(ArraySegment<byte> b, int n0, int n1, int n2) {
        var m0 = n0 + n1;
        var m1 = m0 + n2;
        if (b.Count != m1)
            throw new Exception("wrong buffer size");
        return (b[..n0], b[n0..m0], b[m0..m1]);
    }

    private static (ArraySegment<byte>, ArraySegment<byte>, ArraySegment<byte>) SplitSuffixes(ArraySegment<byte> b, int n1, int n2) {
        return Split(b, b.Count - n1 - n2, n1, n2);
    }

    private static (ArraySegment<byte>, ArraySegment<byte>) SplitSuffix(ArraySegment<byte> b, int n) {
        return Split(b, b.Count - n, n);
    }
}
