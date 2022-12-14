using System.Security.Cryptography;
using System.Text;

namespace WebSocks;

internal class Codec
{
    private const int KeyLen = 16;
    private static readonly int NonceLen = AesCcm.NonceByteSizes.MinSize;
    private static readonly int TagLen = AesCcm.TagByteSizes.MinSize;

    private readonly AesCcm _cipher;
    private readonly RandomNumberGenerator _rng;

    internal Codec(string key)
    {
        var ikm = Encoding.UTF8.GetBytes(key);
        var dkey = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, KeyLen);
        _cipher = new AesCcm(dkey);

        _rng = RandomNumberGenerator.Create();
    }

    internal void Encode(byte[] data, Stream stream)
    {
        var nonce = new byte[NonceLen];
        _rng.GetBytes(nonce);

        var tag = new byte[TagLen];

        var ciphertext = new byte[data.Length];
        _cipher.Encrypt(nonce, data, ciphertext, tag);

        stream.Write(nonce);
        stream.Write(tag);
        stream.Write(BitConverter.GetBytes(ciphertext.Length));
        stream.Write(ciphertext);
    }

    internal byte[] Decode(Stream stream)
    {
        var nonce = Read(stream, NonceLen);
        var tag = Read(stream, TagLen);
        var len = Read(stream, sizeof(int));
        var ciphertext = Read(stream, BitConverter.ToInt32(len));

        var plaintext = new byte[ciphertext.Length];
        _cipher.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }

    private static byte[] Read(Stream stream, int n)
    {
        var buffer = new byte[n];
        var r = stream.Read(buffer);
        if (r != n)
            throw new Exception("incomplete read");
        return buffer;
    }
}