using System.Security.Cryptography;

namespace WebStunnel;

internal sealed record SocketId {
    internal SocketId() {
        Value = (uint)RandomNumberGenerator.GetInt32(1, int.MaxValue);
    }

    internal SocketId(Span<byte> b) {
        Value = BitConverter.ToUInt32(b);
        if (Value == 0)
            throw new Exception("nonzero id required");
    }

    internal static int Size => sizeof(uint);

    internal uint Value { get; }

    internal void Write(Span<byte> x) {
        if (!BitConverter.TryWriteBytes(x, Value))
            throw new Exception("could not write SocketId");
    }

    public override string ToString() {
        return $"{Value:x}";
    }
}

