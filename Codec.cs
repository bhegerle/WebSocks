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
        _key = SHA512.Create().ComputeHash(_key);

        _check = new byte[HashSize];
    }

    internal static ArraySegment<byte> GetAuthSegment(byte[] buffer)
    {
        Array.Fill(buffer, default);
        return buffer.AsSegment()[HashSize..];
    }

    internal ArraySegment<byte> AuthMessage(ArraySegment<byte> seg)
    {
        if (seg.Array == null || seg.Offset != HashSize)
            throw new Exception("invalid segment");

        var combSeg = seg.Array.AsSegment(0, seg.Count + HashSize);
        var hashSeg = combSeg[.. HashSize];

        HMACSHA512.HashData(_key, seg, hashSeg);

        return combSeg;
    }

    internal ArraySegment<byte> VerifyMessage(ArraySegment<byte> seg)
    {
        var hashSeg = seg[.. HashSize];
        var msgSeg = seg[HashSize..];

        HMACSHA512.HashData(_key, msgSeg, _check);

        var eq = true;
        for (var i = 0; i < HashSize; i++) eq = eq && hashSeg[i] == _check[i];

        if (!eq)
            throw new Exception("message could not be verified");

        return msgSeg;
    }
}