using System.Security.Cryptography;

namespace WebStunnel;

internal sealed class Codec {
    private const int keyBytes = 32;
    private const int iterations = 100_000;
    private static readonly HashAlgorithmName hashAlgo = HashAlgorithmName.SHA512;

    private readonly AesGcm aes;
    private readonly byte[] nonce;

    private ulong counter;

    internal Codec(Span<char> key, ReadOnlySpan<byte> salt) {
        var dkey = Rfc2898DeriveBytes.Pbkdf2(key, salt, iterations, hashAlgo, keyBytes);
        aes = new AesGcm(dkey);
        nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
    }

    internal void Tag(ReadOnlySpan<byte> assocData, Span<byte> tag) {
        UpdateNonce();
        aes.Encrypt(nonce, default, default, tag, assocData);
    }

    internal void VerifyTag(ReadOnlySpan<byte> assocData, Span<byte> tag) {
        UpdateNonce();
        aes.Decrypt(nonce, default, tag, default, assocData);
    }

    internal void Encrypt(Span<byte> buffer, Span<byte> tag) {
        UpdateNonce();
        aes.Encrypt(nonce, buffer, buffer, tag);
    }

    internal void Decrypt(Span<byte> buffer, ReadOnlySpan<byte> tag) {
        UpdateNonce();
        aes.Decrypt(nonce, buffer, tag, buffer);
    }

    private void UpdateNonce() {
        BitConverter.TryWriteBytes(nonce, counter);
        counter++;
    }
}