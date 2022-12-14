using System.Security.Cryptography;
using System.Text;

namespace shock;

internal class Codec
{
    private const int KEY_LEN = 16;
    private readonly AesCcm Cipher;

    internal Codec(string key)
    {
        var ikm = Encoding.UTF8.GetBytes(key);
        var dkey = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, KEY_LEN);
        Cipher = new AesCcm(dkey);

        var nonce = new byte[10];
        RandomNumberGenerator.Create().GetBytes(nonce);
    }
}