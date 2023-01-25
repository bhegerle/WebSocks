using System.Security.Cryptography;

namespace WebStunnel;

internal sealed record SocketId {
    internal SocketId() {
        var b = new byte[Size];
        while (Value == 0) {
            RandomNumberGenerator.Fill(b);
            Value = BitConverter.ToUInt32(b);
        }
    }

    internal SocketId(Span<byte> b) {
        Value = BitConverter.ToUInt32(b);
        if (Value == 0)
            throw new Exception("nonzero id required");
    }

    internal static int Size => sizeof(uint);
    internal uint Value { get; }

    internal ArraySegment<byte> GetSegment() {
        return BitConverter.GetBytes(Value).AsSegment();
    }

    public override string ToString() {
        return $"{Value:x}";
    }
}

