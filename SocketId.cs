using System.Security.Cryptography;

namespace WebStunnel;

internal sealed record SocketId {
    internal SocketId() : this(GetRand()) {
    }

    private static uint GetRand() {
        var v = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
        return (uint)(v != 0 ? v : int.MaxValue);
    }

    internal SocketId(uint v) {
        if (v == 0)
            throw new Exception("nonzero id required");
        Value = v;
    }

    internal uint Value { get; }

    public override string ToString() {
        return $"{Value:x}";
    }
}

