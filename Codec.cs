using System.Security.Cryptography;
using System.Text;

namespace shock;

internal class Codec
{
    private const int KEY_LEN = 16;
    private static readonly int NONCE_LEN = AesCcm.NonceByteSizes.MinSize;
    private static readonly int TAG_LEN = AesCcm.TagByteSizes.MinSize;

    private readonly AesCcm Cipher;
    private readonly RandomNumberGenerator Rng;

    internal Codec(string key)
    {
        var ikm = Encoding.UTF8.GetBytes(key);
        var dkey = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, KEY_LEN);
        Cipher = new AesCcm(dkey);

        Rng = RandomNumberGenerator.Create();
    }

    internal void Encode(byte[] data, Stream stream)
    {
        var nonce = new byte[NONCE_LEN];
        Rng.GetBytes(nonce);

        var tag = new byte[TAG_LEN];

        var ciphertext = new byte[data.Length];
        Cipher.Encrypt(nonce, data, ciphertext, tag);

        stream.Write(nonce);
        stream.Write(tag);
        stream.Write(BitConverter.GetBytes(ciphertext.Length));
        stream.Write(ciphertext);
    }

    internal byte[] Decode(Stream stream)
    {
        var nonce = Read(stream, NONCE_LEN);
        var tag = Read(stream, TAG_LEN);
        var len = Read(stream, sizeof(int));
        var ciphertext = Read(stream, BitConverter.ToInt32(len));

        var plaintext = new byte[ciphertext.Length];
        Cipher.Decrypt(nonce, ciphertext, tag, plaintext);

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