using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace EasyPlayscript.DataModel;

/// <summary>
/// Loads and decrypts serialized <see cref="PlayscriptData" /> from a binary blob
/// written at build time by <c>PlayscriptBuildTask</c>.
///
/// <para>
/// The on-disk wire format starts with a single magic byte:
/// </para>
/// <code>
/// ┌──────────┬──────────────┬──────────┬─────────────┐
/// │  1 byte  │  16 bytes    │ 16 bytes │  N bytes    │
/// │  magic   │  salt        │  IV      │  ciphertext │
/// │  0xFE    │  (PBKDF2)    │  (AES)   │  (AES-CBC)  │
/// └──────────┴──────────────┴──────────┴─────────────┘
/// </code>
/// <para>
/// The <c>0xFE</c> magic byte is reserved by the MessagePack specification
/// (never a valid first byte for any standard MessagePack type) and therefore
/// cannot be confused with a raw PlayscriptData payload. The first byte of
/// any real serialized <see cref="PlayscriptData" /> is a map or array header
/// (e.g. <c>0x82</c> for a 2-entry map or <c>0x92</c> for a 2-entry array),
/// never <c>0xFE</c>. See <c>docs/CRYPTOGRAPHY.md</c> for the rationale.
/// </para>
/// <para>
/// The format is intentionally a single version. Any future migration (different
/// KDF, different cipher, different header layout) ships a new magic byte and
/// a new code path in <see cref="Decrypt" />. No in-format version field is
/// reserved because there is no v2 to disambiguate from today.
/// </para>
/// <para>
/// When <paramref name="passphrase" /> is null or empty, the blob is treated as
/// unencrypted (raw MessagePack). The build pipeline produces a passthrough
/// blob when the project has no <c>PlayscriptAesKey</c> set.
/// </para>
/// <para>
/// The encryption uses PBKDF2-HMAC-SHA256 with 100,000 iterations to derive a
/// 32-byte AES-256 key from the passphrase and per-blob salt, then AES-256-CBC
/// with PKCS7 padding. See <c>docs/CRYPTOGRAPHY.md</c> for the design
/// rationale and threat model.
/// </para>
/// </summary>
public static class PlayscriptLoader
{
    /// <summary>
    /// Magic byte that prefixes every encrypted blob. Chosen as a byte value
    /// that is <em>never</em> a valid first byte for any standard MessagePack
    /// type (0xFE is reserved by the spec), so a real <see cref="PlayscriptData" />
    /// can never be mistaken for an encrypted blob.
    /// </summary>
    private const byte MagicByte = 0xFE;

    private const int SaltSize = 16;
    private const int IvSize = 16;
    private const int KeySize = 32; // AES-256
    private const int HeaderSize = 1 + SaltSize + IvSize; // 33 bytes
    private const int Pbkdf2Iterations = 100_000;

    // ── Public entry points ────────────────────────────────────────────────

    /// <summary>
    /// Loads only the <c>Scripts</c> dictionary from a binary blob on disk.
    /// </summary>
    public static Dictionary<string, ScriptBlock> LoadScripts(string path, string? passphrase) => LoadData(path, passphrase).Scripts;

    /// <summary>
    /// Loads only the <c>Texts</c> dictionary from a binary blob on disk.
    /// </summary>
    public static Dictionary<string, TextBlock> LoadTexts(string path, string? passphrase) => LoadData(path, passphrase).Texts;

    /// <summary>
    /// Loads <see cref="ScriptBlock" />s from an in-memory blob. Use this when the
    /// <c>.bin</c> is shipped as an embedded resource or otherwise preloaded.
    /// </summary>
    public static Dictionary<string, ScriptBlock> LoadScripts(byte[] blob, string? passphrase) => LoadData(blob, passphrase).Scripts;

    /// <summary>
    /// Loads <see cref="TextBlock" />s from an in-memory blob. Use this when the
    /// <c>.bin</c> is shipped as an embedded resource or otherwise preloaded.
    /// </summary>
    public static Dictionary<string, TextBlock> LoadTexts(byte[] blob, string? passphrase) => LoadData(blob, passphrase).Texts;

    /// <summary>
    /// Loads <see cref="ScriptBlock" />s from a stream. The stream is read to end.
    /// </summary>
    public static Dictionary<string, ScriptBlock> LoadScripts(Stream stream, string? passphrase) => LoadData(stream, passphrase).Scripts;

    /// <summary>
    /// Loads <see cref="TextBlock" />s from a stream. The stream is read to end.
    /// </summary>
    public static Dictionary<string, TextBlock> LoadTexts(Stream stream, string? passphrase) => LoadData(stream, passphrase).Texts;

    /// <summary>
    /// Loads the full <see cref="PlayscriptData" /> graph from a binary blob on disk.
    /// </summary>
    public static PlayscriptData LoadData(string path, string? passphrase)
    {
        var bytes = File.ReadAllBytes(path);
        return LoadData(bytes, passphrase);
    }

    /// <summary>
    /// Loads the full <see cref="PlayscriptData" /> graph from an in-memory blob.
    /// </summary>
    public static PlayscriptData LoadData(byte[] blob, string? passphrase)
    {
        var plaintext = Decrypt(blob, passphrase);
        return MessagePackSerializer.Deserialize<PlayscriptData>(plaintext);
    }

    /// <summary>
    /// Loads the full <see cref="PlayscriptData" /> graph from a stream.
    /// </summary>
    public static PlayscriptData LoadData(Stream stream, string? passphrase)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return LoadData(ms.ToArray(), passphrase);
    }

    // ── Encryption / decryption ────────────────────────────────────────────

    /// <summary>
    /// Encrypts <paramref name="plaintext" /> with the wire format described
    /// in the class summary. When <paramref name="passphrase" /> is null or
    /// empty, returns the input unchanged (no encryption, no header).
    /// </summary>
    public static byte[] Encrypt(byte[] plaintext, string? passphrase)
    {
        if (string.IsNullOrEmpty(passphrase))
            return plaintext;

        var salt = new byte[SaltSize];
        var iv = new byte[IvSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
            rng.GetBytes(iv);
        }

        var key = DeriveKey(passphrase!, salt);
        var ciphertext = AesEncrypt(plaintext, key, iv);

        var blob = new byte[HeaderSize + ciphertext.Length];
        blob[0] = MagicByte;
        Buffer.BlockCopy(salt, 0, blob, 1, SaltSize);
        Buffer.BlockCopy(iv, 0, blob, 1 + SaltSize, IvSize);
        Buffer.BlockCopy(ciphertext, 0, blob, HeaderSize, ciphertext.Length);

        Array.Clear(key, 0, key.Length);
        return blob;
    }

    /// <summary>
    /// Decrypts a blob produced by <see cref="Encrypt" />. The first byte is
    /// sniffed: if it equals <see cref="MagicByte" />, the blob is decrypted
    /// with the passphrase; otherwise the blob is returned unchanged (raw
    /// unencrypted passthrough).
    /// </summary>
    /// <exception cref="CryptographicException">
    /// Thrown when the passphrase is wrong, the blob is corrupted, or the
    /// padding is invalid.
    /// </exception>
    public static byte[] Decrypt(byte[] blob, string? passphrase)
    {
        if (blob == null || blob.Length == 0)
            throw new InvalidDataException("PlayscriptLoader.Decrypt: blob is null or empty.");

        // Sniff: any blob whose first byte is not the magic byte is treated
        // as raw unencrypted bytes and returned as-is. This lets the loader
        // handle raw MessagePack payloads produced without going through
        // Encrypt (e.g. test fixtures, dev builds).
        if (blob[0] != MagicByte)
            return blob;

        if (blob.Length <= HeaderSize)
            throw new InvalidDataException(
                $"PlayscriptLoader.Decrypt: blob is too small to contain a magic + salt + IV header " +
                $"plus at least one byte of ciphertext (got {blob.Length} bytes, need at least {HeaderSize + 1}).");

        if (string.IsNullOrEmpty(passphrase))
            throw new CryptographicException(
                "PlayscriptLoader.Decrypt: blob is encrypted but no passphrase was provided.");

        var salt = new byte[SaltSize];
        var iv = new byte[IvSize];
        Buffer.BlockCopy(blob, 1, salt, 0, SaltSize);
        Buffer.BlockCopy(blob, 1 + SaltSize, iv, 0, IvSize);

        var ciphertextLength = blob.Length - HeaderSize;
        var ciphertext = new byte[ciphertextLength];
        Buffer.BlockCopy(blob, HeaderSize, ciphertext, 0, ciphertextLength);

        var key = DeriveKey(passphrase!, salt);
        try
        {
            return AesDecrypt(ciphertext, key, iv);
        }
        finally
        {
            Array.Clear(key, 0, key.Length);
        }
    }

    // ── Internals ──────────────────────────────────────────────────────────

    private static byte[] DeriveKey(string passphrase, byte[] salt)
    {
        return KeyDerivation.Pbkdf2(
            password: passphrase,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: Pbkdf2Iterations,
            numBytesRequested: KeySize);
    }

    private static byte[] AesEncrypt(byte[] plaintext, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
    }

    private static byte[] AesDecrypt(byte[] ciphertext, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
    }
}
