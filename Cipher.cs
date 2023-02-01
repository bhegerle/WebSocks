using System.Security.Cryptography;

namespace WebStunnel;

internal class Cipher {
    private const int keyBytes = 32;
    private const int iterations = 1_000_000;
    private static readonly HashAlgorithmName hashAlgo = HashAlgorithmName.SHA512;

    private readonly AesGcm aes;
    private readonly byte[] nonce;

    private ulong counter;

    internal Cipher(Span<char> key, ReadOnlySpan<byte> salt) {
        var dkey = Rfc2898DeriveBytes.Pbkdf2(key, salt, iterations, hashAlgo, keyBytes);
        aes = new AesGcm(dkey);
        nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
    }

    internal void Tag(ReadOnlySpan<byte> assocData, Span<byte> tag) {
        Encrypt(Span<byte>.Empty, assocData, Span<byte>.Empty, tag);
    }

    internal void VerifyTag(ReadOnlySpan<byte> assocData, Span<byte> tag) {
        Decrypt(Span<byte>.Empty, tag, assocData, Span<byte>.Empty);
    }

    internal void Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> assocData, Span<byte> ciphertext, Span<byte> tag) {
        UpdateNonce();
        aes.Encrypt(nonce, plaintext, ciphertext, tag, assocData);
    }

    internal void Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, ReadOnlySpan<byte> assocData, Span<byte> plaintext) {
        UpdateNonce();
        aes.Decrypt(nonce, ciphertext, tag, plaintext, assocData);
    }

    private void UpdateNonce() {
        BitConverter.TryWriteBytes(nonce, counter);
        counter++;
    }
}