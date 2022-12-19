using System.Security.Cryptography;
using System.Text;

namespace WebStunnel;

internal class Codec
{
    private const int HashSize = 512 / 8;
    private readonly byte[] _key, _check, _auth, _verify;

    internal Codec(Config config)
    {
        if (string.IsNullOrEmpty(config.Key))
            throw new Exception("key required");

        _key = Encoding.UTF8.GetBytes(config.Key);

        using var sha = SHA512.Create();
        _key = sha.ComputeHash(_key);

        _check = new byte[HashSize];
        _auth = new byte[HashSize];
        _verify = new byte[HashSize];
    }

    internal ArraySegment<byte> AuthMessage(ArraySegment<byte> seg)
    {
        var msg = new Frame(seg, true);
        _auth.AsSpan().CopyTo(msg.Suffix);

        HMACSHA512.HashData(_key, msg.HmacInput, _auth);
        _auth.AsSpan().CopyTo(msg.Hmac);

        Dump("auth", msg);

        return msg.Complete;
    }

    internal ArraySegment<byte> VerifyMessage(ArraySegment<byte> seg)
    {
        var msg = new Frame(seg, false);

        Dump("verify", msg);

        if (!Utils.ConjEqual(msg.Suffix, _verify))
            throw new Exception("unexpected suffix");
        
        HMACSHA512.HashData(_key, msg.HmacInput, _verify);

        if(!Utils.ConjEqual(msg.Hmac, _verify))
            throw new Exception("invalid HMAC");

        return msg.Message;
    }

    private void Dump(string auth, Frame msg)
    {
        Console.Write($"{auth}\t");
        foreach (var x in msg.Complete)
            Console.Write($"{x:x2}");
        Console.WriteLine();
    }

    private struct Frame
    {
        private const int H2 = 2 * HashSize;

        internal Frame(ArraySegment<byte> x, bool extend)
        {
            if (extend)
                x = x.Array.AsSegment(x.Offset, x.Count + H2);

            Complete = x;
        }

        internal readonly ArraySegment<byte> Complete;
        internal ArraySegment<byte> Message => Complete[..^H2];
        internal ArraySegment<byte> Suffix => Complete[^H2..^HashSize];
        internal ArraySegment<byte> HmacInput => Complete[..^HashSize];
        internal ArraySegment<byte> Hmac => Complete[^HashSize..];
    }
}