using System.Security.Cryptography;
using System.Text;

namespace WebStunnel;

internal class Codec
{
    private const int HashSize = 512 / 8;
    private readonly byte[] _key, _check;

    internal Codec(Config config)
    {
        if (string.IsNullOrEmpty(config.Key))
            throw new Exception("key required");

        _key = Encoding.UTF8.GetBytes(config.Key);

        using var sha = SHA512.Create();
        _key = sha.ComputeHash(_key);

        _check = new byte[HashSize];
    }

    internal ArraySegment<byte> AuthMessage(ArraySegment<byte> seg)
    {
        return seg;
        if (seg.Array == null || seg.Offset != HashSize)
            throw new Exception("invalid segment");

        var combSeg = seg.Array.AsSegment(0, seg.Count + HashSize);
        var hashSeg = combSeg[.. HashSize];

        HMACSHA512.HashData(_key, seg, hashSeg);

        return combSeg;
    }

    internal ArraySegment<byte> VerifyMessage(ArraySegment<byte> seg)
    {
        return seg;
        var hashSeg = seg[.. HashSize];
        var msgSeg = seg[HashSize..];

        HMACSHA512.HashData(_key, msgSeg, _check);

        var eq = Utils.ConjEqual(hashSeg, _check);

        Array.Fill(_check, default);

        if (!eq)
            throw new Exception("message could not be verified");

        return msgSeg;
    }
}