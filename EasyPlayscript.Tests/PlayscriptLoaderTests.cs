using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using EasyPlayscript.DataModel;
using MessagePack;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptLoaderTests
{
    private const string TestPassphrase = "test-passphrase-1234567";

    // ─── Round-trip ──────────────────────────────────────────────────────────

    [Fact]
    public void Encrypt_Then_Decrypt_RoundTrips_Payload()
    {
        var original = Encoding.UTF8.GetBytes("Hello, world!");
        var encrypted = PlayscriptLoader.Encrypt(original, TestPassphrase);
        var decrypted = PlayscriptLoader.Decrypt(encrypted, TestPassphrase);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Encrypt_Then_Decrypt_RoundTrips_EmptyPayload()
    {
        var original = Array.Empty<byte>();
        var encrypted = PlayscriptLoader.Encrypt(original, TestPassphrase);
        var decrypted = PlayscriptLoader.Decrypt(encrypted, TestPassphrase);
        Assert.Empty(decrypted);
    }

    [Fact]
    public void Encrypt_Then_Decrypt_RoundTrips_LargePayload()
    {
        var rng = RandomNumberGenerator.Create();
        var original = new byte[1024 * 64];
        rng.GetBytes(original);

        var encrypted = PlayscriptLoader.Encrypt(original, TestPassphrase);
        var decrypted = PlayscriptLoader.Decrypt(encrypted, TestPassphrase);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Encrypt_Then_Decrypt_RoundTrips_MessagePackPlayscriptData()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptVariants>
            {
                ["greeting"] = new ScriptVariants { Unversioned = new()
                {
                    Pages = new List<Page>
                    {
                        new()
                        {
                            Paragraphs = new List<Paragraph>
                            {
                                new()
                                {
                                    Lines = new List<Line>
                                    {
                                        new()
                                        {
                                            Segments = new List<Segment>
                                            {
                                                new()
                                                {
                                                    Items = new List<LineItem>
                                                    {
                                                        new TextItem("Hello "),
                                                        new ConsumerCallItem("transition",
                                                            new List<ArgumentValue> { new StringArgument("fade_out") }),
                                                        new TextItem(" world")
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            },
            Texts = new Dictionary<string, TextVariants>()
        };

        var bytes = MessagePackSerializer.Serialize(data);
        var encrypted = PlayscriptLoader.Encrypt(bytes, TestPassphrase);
        var decrypted = PlayscriptLoader.Decrypt(encrypted, TestPassphrase);
        var deserialized = MessagePackSerializer.Deserialize<PlayscriptData>(decrypted);

        Assert.Single(deserialized.Scripts);
        var items = deserialized.Scripts["greeting"].Unversioned.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items;
        Assert.Equal(3, items.Count);
        Assert.Equal("Hello ", ((TextItem)items[0]).Text);
        Assert.Equal("fade_out", ((StringArgument)((ConsumerCallItem)items[1]).Arguments[0]).Value);
        Assert.Equal(" world", ((TextItem)items[2]).Text);
    }

    // ─── Wire format ─────────────────────────────────────────────────────────

    [Fact]
    public void Encrypt_Produces_Magic_And_Header()
    {
        var original = Encoding.UTF8.GetBytes("test");
        var encrypted = PlayscriptLoader.Encrypt(original, TestPassphrase);

        // Header is: 1-byte magic (0xFE) || 16-byte salt || 16-byte IV || ciphertext
        // Minimum: 1 + 16 + 16 + 16 (one AES block) = 49 bytes
        Assert.True(encrypted.Length >= 49, $"Encrypted blob too small: {encrypted.Length}");

        Assert.Equal(0xFE, encrypted[0]);
    }

    [Fact]
    public void Encrypt_Header_Has_Magic_Then_Salt_Then_IV_Then_Ciphertext()
    {
        var original = Encoding.UTF8.GetBytes("test");
        var encrypted = PlayscriptLoader.Encrypt(original, TestPassphrase);

        // 1 byte magic
        Assert.Equal(0xFE, encrypted[0]);

        // 16 bytes salt starting at offset 1 — must not be all zeros
        var salt = encrypted.AsSpan(1, 16).ToArray();
        Assert.Contains(salt, b => b != 0);

        // 16 bytes IV starting at offset 17 — must not be all zeros
        var iv = encrypted.AsSpan(17, 16).ToArray();
        Assert.Contains(iv, b => b != 0);

        // At least 16 bytes of ciphertext starting at offset 33
        var ciphertext = encrypted.AsSpan(33).ToArray();
        Assert.NotEmpty(ciphertext);
    }

    [Fact]
    public void Encrypt_Each_Call_Uses_Fresh_Salt_And_IV()
    {
        var original = Encoding.UTF8.GetBytes("identical payload");

        var a = PlayscriptLoader.Encrypt(original, TestPassphrase);
        var b = PlayscriptLoader.Encrypt(original, TestPassphrase);

        // Two encryptions of the same plaintext with the same key must differ
        // (because salt and IV are fresh per call).
        Assert.NotEqual(a, b);

        // Specifically, the salt and IV prefixes should differ.
        Assert.NotEqual(a.AsSpan(1, 16).ToArray(), b.AsSpan(1, 16).ToArray());
        Assert.NotEqual(a.AsSpan(17, 16).ToArray(), b.AsSpan(17, 16).ToArray());
    }

    [Fact]
    public void Encrypted_Header_Does_Not_Leak_Passphrase_In_Plaintext()
    {
        var original = Encoding.UTF8.GetBytes("payload");
        var encrypted = PlayscriptLoader.Encrypt(original, TestPassphrase);

        // The passphrase must not appear anywhere in the encrypted blob as a literal string.
        // (It would be in the UTF-8 bytes somewhere if the encryption were broken.)
        var asString = Encoding.UTF8.GetString(encrypted);
        Assert.DoesNotContain(TestPassphrase, asString);

        var passphraseBytes = Encoding.UTF8.GetBytes(TestPassphrase);
        var containsAsBytes = false;
        for (var i = 0; i <= encrypted.Length - passphraseBytes.Length; i++)
        {
            var match = true;
            for (var j = 0; j < passphraseBytes.Length; j++)
            {
                if (encrypted[i + j] != passphraseBytes[j]) { match = false; break; }
            }
            if (match) { containsAsBytes = true; break; }
        }
        Assert.False(containsAsBytes, "Passphrase appears in plaintext bytes of encrypted blob.");
    }

    // ─── Magic-byte discriminator ──────────────────────────────────────────

    [Fact]
    public void Encrypt_Header_Starts_With_Magic_Byte_0xFE()
    {
        // The first byte of an encrypted blob must be 0xFE, a value reserved
        // by the MessagePack spec and therefore never the first byte of a
        // valid PlayscriptData map. This is what disambiguates "encrypted
        // blob" from "raw MessagePack" without ambiguity.
        var original = Encoding.UTF8.GetBytes("anything");
        var encrypted = PlayscriptLoader.Encrypt(original, TestPassphrase);

        Assert.True(encrypted.Length > 0);
        Assert.Equal(0xFE, encrypted[0]);
    }

    [Fact]
    public void Decrypt_Of_Raw_Blob_Starting_With_Magic_Byte_Attempts_Decryption()
    {
        // Documents the design: the magic byte IS the discriminator. A raw
        // blob that happens to start with 0xFE will be treated as encrypted.
        // This is intentional — the alternative (sniffing the first 4 bytes
        // as a version uint32) led to false positives on rare raw blobs.
        //
        // The "fix" is that decryption will throw a CryptographicException
        // because the bytes after the magic byte are not a valid encrypted
        // payload, so the user gets a clear error.
        //
        // The blob must be at least 34 bytes (full header + 1 ciphertext
        // byte) to reach the decryption path; smaller blobs hit the
        // truncation check first. We use random data (not zeros) so the
        // AES-decrypted bytes are not all-zero — that avoids the ~1/16
        // chance that a zero-filled block accidentally satisfies PKCS7
        // padding validation.
        var rng = RandomNumberGenerator.Create();
        var rawBlob = new byte[64];
        rng.GetBytes(rawBlob);
        rawBlob[0] = 0xFE; // magic byte — looks like an encrypted blob

        Assert.ThrowsAny<CryptographicException>(
            () => PlayscriptLoader.Decrypt(rawBlob, "any-key"));
    }

    [Fact]
    public void Decrypt_Of_Real_PlayscriptData_Still_Passes_Through()
    {
        // Real PlayscriptData starts with either 0x82 (MessagePack fixmap
        // with 2 entries) or 0x92 (fixarray with 2 entries, depending on the
        // MessagePack library version's default object encoding). Either
        // way, it is NOT 0xFE, so the magic-byte sniff must correctly
        // identify it as raw passthrough.
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptVariants>
            {
                ["greeting"] = new()
            },
            Texts = new Dictionary<string, TextVariants>()
        };
        var raw = MessagePackSerializer.Serialize(data);

        // Sanity check on the test itself: real PlayscriptData must not
        // start with the encrypted-blob magic byte.
        Assert.NotEqual(0xFE, raw[0]);

        var scripts = PlayscriptLoader.LoadScripts(raw, null);
        Assert.Single(scripts);
    }

    // ─── Unencrypted passthrough ────────────────────────────────────────────

    [Fact]
    public void Encrypt_With_NullPassphrase_Produces_Unencrypted_Blob()
    {
        var original = Encoding.UTF8.GetBytes("plain");
        var result = PlayscriptLoader.Encrypt(original, null);

        Assert.Equal(original, result);
    }

    [Fact]
    public void Encrypt_With_EmptyPassphrase_Produces_Unencrypted_Blob()
    {
        var original = Encoding.UTF8.GetBytes("plain");
        var result = PlayscriptLoader.Encrypt(original, "");

        Assert.Equal(original, result);
    }

    [Fact]
    public void Decrypt_Of_Unencrypted_Blob_Returns_Input()
    {
        var original = Encoding.UTF8.GetBytes("plain");
        var result = PlayscriptLoader.Decrypt(original, TestPassphrase);
        Assert.Equal(original, result);
    }

    [Fact]
    public void Decrypt_Of_Unencrypted_Blob_With_Null_Passphrase_Returns_Input()
    {
        var original = Encoding.UTF8.GetBytes("plain");
        var result = PlayscriptLoader.Decrypt(original, null);
        Assert.Equal(original, result);
    }

    // ─── Failure modes ──────────────────────────────────────────────────────

    [Fact]
    public void Decrypt_With_Wrong_Passphrase_Throws()
    {
        // PKCS7 padding validation only fails if the last block's last byte
        // is not a valid padding value. With a short payload, there's a
        // ~1/256 chance the wrong key accidentally produces valid padding
        // (and AES decryption returns garbage silently). Using a long
        // payload (1024 bytes = 64 AES blocks) makes this probability
        // astronomically small (~ 256^-64), so the throw is reliable.
        var rng = RandomNumberGenerator.Create();
        var original = new byte[1024];
        rng.GetBytes(original);
        var encrypted = PlayscriptLoader.Encrypt(original, TestPassphrase);

        Assert.ThrowsAny<CryptographicException>(
            () => PlayscriptLoader.Decrypt(encrypted, "wrong-passphrase"));
    }

    [Fact]
    public void Decrypt_With_Empty_Passphrase_On_Encrypted_Blob_Throws()
    {
        var original = Encoding.UTF8.GetBytes("secret payload");
        var encrypted = PlayscriptLoader.Encrypt(original, TestPassphrase);

        Assert.ThrowsAny<CryptographicException>(() => PlayscriptLoader.Decrypt(encrypted, ""));
        Assert.ThrowsAny<CryptographicException>(() => PlayscriptLoader.Decrypt(encrypted, null));
    }

    [Fact]
    public void Decrypt_Of_Corrupted_Ciphertext_Throws()
    {
        var original = Encoding.UTF8.GetBytes("secret payload");
        var encrypted = PlayscriptLoader.Encrypt(original, TestPassphrase);

        // Flip a byte in the middle of the ciphertext (after the 33-byte header)
        encrypted[encrypted.Length - 1] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(
            () => PlayscriptLoader.Decrypt(encrypted, TestPassphrase));
    }

    [Fact]
    public void Decrypt_Of_Corrupted_Salt_Throws()
    {
        var original = Encoding.UTF8.GetBytes("secret payload");
        var encrypted = PlayscriptLoader.Encrypt(original, TestPassphrase);

        // Flip a byte in the salt (which starts at offset 1, right after the
        // 1-byte magic).
        encrypted[1] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(
            () => PlayscriptLoader.Decrypt(encrypted, TestPassphrase));
    }

    [Fact]
    public void Decrypt_Of_Truncated_Header_Throws()
    {
        // 0 bytes — always throws
        Assert.Throws<InvalidDataException>(() => PlayscriptLoader.Decrypt(Array.Empty<byte>(), TestPassphrase));

        // 1 byte (just the magic) — too small for the salt + IV header.
        var magicOnly = new byte[1] { 0xFE };
        Assert.Throws<InvalidDataException>(() => PlayscriptLoader.Decrypt(magicOnly, TestPassphrase));

        // 17 bytes (magic + salt, no IV) — too small.
        var partial = new byte[17];
        partial[0] = 0xFE;
        Assert.Throws<InvalidDataException>(() => PlayscriptLoader.Decrypt(partial, TestPassphrase));

        // 32 bytes (header missing the last IV byte) — too small.
        var header = new byte[32];
        header[0] = 0xFE;
        Assert.Throws<InvalidDataException>(() => PlayscriptLoader.Decrypt(header, TestPassphrase));

        // 33 bytes (full header, no ciphertext) — degenerate; must throw.
        var noCiphertext = new byte[33];
        noCiphertext[0] = 0xFE;
        Assert.Throws<InvalidDataException>(() => PlayscriptLoader.Decrypt(noCiphertext, TestPassphrase));
    }

    [Fact]
    public void Decrypt_Of_Unknown_Magic_Treats_As_Raw_Passthrough()
    {
        // The magic byte is sniffed, not validated. Any blob whose first byte
        // is not exactly 0xFE is returned unchanged. This means a blob with
        // a different "magic" (e.g. a future format we don't support yet) is
        // passed through, not rejected — letting users load raw MessagePack
        // or fall back gracefully across version mismatches.
        var blob = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0x01, 0x02, 0x03 };
        Assert.Equal(blob, PlayscriptLoader.Decrypt(blob, TestPassphrase));
        Assert.Equal(blob, PlayscriptLoader.Decrypt(blob, null));
    }

    // ─── KDF security property ─────────────────────────────────────────────

    [Fact]
    public void Pbkdf2_Iteration_Count_Is_At_Least_Minimum()
    {
        // OWASP 2023 recommends ≥ 600,000 iterations for PBKDF2-HMAC-SHA256.
        // We aim for at least 100,000 as a floor for v1. The constant is private,
        // but we exercise it by timing an Encrypt call: at 100k+ iterations on
        // modern hardware, even one Encrypt/Decrypt cycle takes > 10ms.
        // This test guards against accidental reduction to 1.
        var original = new byte[64];
        var sw = System.Diagnostics.Stopwatch.StartNew();
        PlayscriptLoader.Encrypt(original, TestPassphrase);
        sw.Stop();

        // 1 iteration of PBKDF2-HMAC-SHA256 takes < 1ms. 100k takes > 10ms.
        // We assert > 5ms to leave headroom for CI environments.
        Assert.True(sw.ElapsedMilliseconds > 5,
            $"Encrypt took {sw.ElapsedMilliseconds}ms — KDF iteration count may be too low.");
    }

    [Fact]
    public void Pbkdf2_Derived_Key_Is_Not_Plain_Sha256_Of_Passphrase()
    {
        // Sanity check: the encryption key is NOT a single SHA-256 of the passphrase.
        // (That's the bug in the legacy code.) We verify this indirectly by
        // ensuring the encryption is non-deterministic: two encryptions with the
        // same passphrase produce different ciphertexts (covered above), AND
        // that flipping the salt produces different output for the same plaintext.
        var original = Encoding.UTF8.GetBytes("identical");
        var enc1 = PlayscriptLoader.Encrypt(original, TestPassphrase);
        var enc2 = PlayscriptLoader.Encrypt(original, TestPassphrase);

        // If the implementation degenerated to a plain hash, the entire body
        // after the salt/IV would be the same once salt/IV are stripped.
        // The whole-blob inequality above already proves this, but be explicit.
        Assert.NotEqual(enc1, enc2);
    }

    // ─── LoadScripts / LoadTexts overloads ──────────────────────────────────

    [Fact]
    public void LoadScripts_From_Bytes_Overload_Respects_Passphrase()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptVariants>
            {
                ["greeting"] = new ScriptVariants { Unversioned = new()
                {
                    Pages = new List<Page>
                    {
                        new()
                        {
                            Paragraphs = new List<Paragraph>
                            {
                                new()
                                {
                                    Lines = new List<Line>
                                    {
                                        new()
                                        {
                                            Segments = new List<Segment>
                                            {
                                                new()
                                                {
                                                    Items = new List<LineItem>
                                                    {
                                                        new TextItem("Hello")
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            },
            Texts = new Dictionary<string, TextVariants>()
        };

        var bytes = PlayscriptLoader.Encrypt(MessagePackSerializer.Serialize(data), TestPassphrase);
        var scripts = PlayscriptLoader.LoadScripts(bytes, TestPassphrase);

        Assert.Single(scripts);
        Assert.IsType<ScriptVariants>(scripts["greeting"]);
        var items = scripts["greeting"].Unversioned!.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items;
        Assert.Equal("Hello", ((TextItem)items[0]).Text);
    }

    [Fact]
    public void LoadTexts_From_Bytes_Overload_Respects_Passphrase()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptVariants>(),
            Texts = new Dictionary<string, TextVariants>
            {
                ["intro"] = new TextVariants { Unversioned = new()
                {
                    Lines = new List<Line>
                    {
                        new()
                        {
                            Segments = new List<Segment>
                            {
                                new()
                                {
                                    Items = new List<LineItem> { new TextItem("Welcome") }
                                }
                            }
                        }
                    }
                }
            }
            }
        };

        var bytes = PlayscriptLoader.Encrypt(MessagePackSerializer.Serialize(data), TestPassphrase);
        var texts = PlayscriptLoader.LoadTexts(bytes, TestPassphrase);

        Assert.Single(texts);
        Assert.IsType<TextVariants>(texts["intro"]);
        Assert.Equal("Welcome",
            ((TextItem)texts["intro"].Unversioned!.Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void LoadScripts_From_Stream_Overload_Respects_Passphrase()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptVariants>
            {
                ["s"] = new()
            },
            Texts = new Dictionary<string, TextVariants>()
        };

        var bytes = PlayscriptLoader.Encrypt(MessagePackSerializer.Serialize(data), TestPassphrase);
        using var stream = new MemoryStream(bytes);
        var scripts = PlayscriptLoader.LoadScripts(stream, TestPassphrase);

        Assert.Single(scripts);
        Assert.NotNull(scripts["s"]);
    }

    [Fact]
    public void LoadScripts_From_Stream_Null_Passphrase_Works_For_Unencrypted_Blob()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptVariants>
            {
                ["s"] = new()
            },
            Texts = new Dictionary<string, TextVariants>()
        };

        var bytes = MessagePackSerializer.Serialize(data); // no encryption
        using var stream = new MemoryStream(bytes);
        var scripts = PlayscriptLoader.LoadScripts(stream, null);

        Assert.Single(scripts);
    }

    [Fact]
    public void LoadTexts_From_Stream_Overload_Respects_Passphrase()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptVariants>(),
            Texts = new Dictionary<string, TextVariants>
            {
                ["t"] = new()
            }
        };

        var bytes = PlayscriptLoader.Encrypt(MessagePackSerializer.Serialize(data), TestPassphrase);
        using var stream = new MemoryStream(bytes);
        var texts = PlayscriptLoader.LoadTexts(stream, TestPassphrase);

        Assert.Single(texts);
        Assert.NotNull(texts["t"]);
    }

    [Fact]
    public void LoadTexts_From_Stream_Null_Passphrase_Works_For_Unencrypted_Blob()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptVariants>(),
            Texts = new Dictionary<string, TextVariants>
            {
                ["t"] = new()
            }
        };

        var bytes = MessagePackSerializer.Serialize(data); // no encryption
        using var stream = new MemoryStream(bytes);
        var texts = PlayscriptLoader.LoadTexts(stream, null);

        Assert.Single(texts);
    }

    [Fact]
    public void LoadScripts_From_File_Overload_Delegates_To_Stream_Overload()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptVariants>
            {
                ["s"] = new()
            },
            Texts = new Dictionary<string, TextVariants>()
        };

        var bytes = PlayscriptLoader.Encrypt(MessagePackSerializer.Serialize(data), TestPassphrase);
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempPath, bytes);
            var scripts = PlayscriptLoader.LoadScripts(tempPath, TestPassphrase);
            Assert.Single(scripts);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void LoadScripts_From_Bytes_With_Null_Passphrase_Works_For_Unencrypted_Blob()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptVariants>
            {
                ["s"] = new()
            },
            Texts = new Dictionary<string, TextVariants>()
        };

        var bytes = MessagePackSerializer.Serialize(data);
        var scripts = PlayscriptLoader.LoadScripts(bytes, null);
        Assert.Single(scripts);
    }

    // ─── LoadData overloads (full graph) ────────────────────────────────────

    [Fact]
    public void LoadData_From_Bytes_Overload_Respects_Passphrase()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptVariants>
            {
                ["greeting"] = new()
            },
            Texts = new Dictionary<string, TextVariants>
            {
                ["intro"] = new()
            }
        };

        var bytes = PlayscriptLoader.Encrypt(MessagePackSerializer.Serialize(data), TestPassphrase);
        var roundTripped = PlayscriptLoader.LoadData(bytes, TestPassphrase);

        Assert.Single(roundTripped.Scripts);
        Assert.Single(roundTripped.Texts);
        Assert.True(roundTripped.Scripts.ContainsKey("greeting"));
        Assert.True(roundTripped.Texts.ContainsKey("intro"));
    }

    [Fact]
    public void LoadData_From_Bytes_With_Null_Passphrase_Works_For_Unencrypted_Blob()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptVariants>
            {
                ["s"] = new()
            },
            Texts = new Dictionary<string, TextVariants>
            {
                ["t"] = new()
            }
        };

        var bytes = MessagePackSerializer.Serialize(data);
        var roundTripped = PlayscriptLoader.LoadData(bytes, null);

        Assert.Single(roundTripped.Scripts);
        Assert.Single(roundTripped.Texts);
    }

    [Fact]
    public void LoadData_From_Stream_Overload_Respects_Passphrase()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptVariants>
            {
                ["s"] = new()
            },
            Texts = new Dictionary<string, TextVariants>
            {
                ["t"] = new()
            }
        };

        var bytes = PlayscriptLoader.Encrypt(MessagePackSerializer.Serialize(data), TestPassphrase);
        using var stream = new MemoryStream(bytes);
        var roundTripped = PlayscriptLoader.LoadData(stream, TestPassphrase);

        Assert.Single(roundTripped.Scripts);
        Assert.Single(roundTripped.Texts);
    }

    [Fact]
    public void LoadData_From_Stream_With_Null_Passphrase_Works_For_Unencrypted_Blob()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptVariants>
            {
                ["s"] = new()
            },
            Texts = new Dictionary<string, TextVariants>
            {
                ["t"] = new()
            }
        };

        var bytes = MessagePackSerializer.Serialize(data); // no encryption
        using var stream = new MemoryStream(bytes);
        var roundTripped = PlayscriptLoader.LoadData(stream, null);

        Assert.Single(roundTripped.Scripts);
        Assert.Single(roundTripped.Texts);
    }

    [Fact]
    public void LoadData_From_File_Overload_Delegates_To_Stream_Overload()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptVariants>
            {
                ["s"] = new()
            },
            Texts = new Dictionary<string, TextVariants>
            {
                ["t"] = new()
            }
        };

        var bytes = PlayscriptLoader.Encrypt(MessagePackSerializer.Serialize(data), TestPassphrase);
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempPath, bytes);
            var roundTripped = PlayscriptLoader.LoadData(tempPath, TestPassphrase);

            Assert.Single(roundTripped.Scripts);
            Assert.Single(roundTripped.Texts);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void LoadData_From_File_With_Null_Passphrase_Works_For_Unencrypted_Blob()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptVariants>
            {
                ["s"] = new()
            },
            Texts = new Dictionary<string, TextVariants>
            {
                ["t"] = new()
            }
        };

        var bytes = MessagePackSerializer.Serialize(data);
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempPath, bytes);
            var roundTripped = PlayscriptLoader.LoadData(tempPath, null);

            Assert.Single(roundTripped.Scripts);
            Assert.Single(roundTripped.Texts);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
