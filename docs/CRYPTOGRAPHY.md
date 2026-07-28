# Cryptography — Design Notes

This document captures the design decisions for the cryptography refactor in `EasyPlayscript.Core/DataModel/PlayscriptLoader.cs`. The audit in `RELEASE-READINESS.md` flagged the old implementation as borderline broken; this is the fix.

## Threat model

We are defending against:

1. **A player with a hex editor** opening `playscripts.bin` in a shipped game. Goal: casual snooping shouldn't be trivial.
2. **A player with a .NET decompiler** opening `Game.dll`. Goal: the decryption key must not live in the binary.

We are **not** defending against:

- A determined attacker with a debugger, memory scanner, or kernel-level access.
- A user who can MITM the live game (TLS / DRM is someone else's job).

The point of this layer is to raise the cost of casual snooping, not to provide DRM. The docs in `README.md` will be honest about that.

## Wire format

The encrypted `.bin` file is a single binary blob with this layout:

```
┌──────────┬──────────────┬──────────┬─────────────┐
│  1 byte  │  16 bytes    │ 16 bytes │  N bytes    │
│  magic   │  salt        │  IV      │  ciphertext │
│  0xFE    │  (PBKDF2)    │  (AES)   │  (AES-CBC)  │
└──────────┴──────────────┴──────────┴─────────────┘
```

- `magic` — `0xFE`. Reserved by the MessagePack specification (never a valid
  first byte for any standard MessagePack type), so a real serialized
  `PlayscriptData` (which always starts with a map or array header) can never
  be mistaken for an encrypted blob. This is the **discriminator** — `Decrypt`
  sniffs on this byte.
- `salt` — 16 random bytes, generated fresh at every encryption.
- `IV` — 16 random bytes, generated fresh at every encryption (AES-CBC requirement).
- `ciphertext` — `AES-256-CBC(plaintext, key, IV)` with PKCS7 padding.

Total header overhead: 33 bytes.

When `passphrase` is null/empty, no header is written and the payload is the
raw plaintext (no encryption). This preserves the "dev build without a key"
workflow. The first byte of a raw `PlayscriptData` is always a map or array
header (`0x82` for a 2-entry map, `0x92` for a 2-entry array, depending on
MessagePack-CSharp's default object encoding) — never `0xFE` — so the
discriminator cleanly distinguishes "encrypted" from "raw."

### Why a magic byte

`0xFE` is reserved by the MessagePack spec, so it can never appear as the
first byte of a valid serialized `PlayscriptData` (always a map or array
header). That makes it an unambiguous discriminator:

- Real `PlayscriptData`: never mis-routed.
- Wrong file: 1/256 chance of being mis-routed as encrypted, still fails fast
  with a clear error.
- The failure mode is loud (decryption error or invalid padding), not silent
  corruption.

### Future format migrations

The format is intentionally a single version. If the KDF, cipher, or header
layout ever needs to change, the migration is:

1. Choose a new magic byte (e.g. `0xFD`) from the MessagePack reserved range.
2. Add a new code path in `Decrypt` that handles the new magic.
3. Old files (magic `0xFE`) still load via the existing path.

No in-format version field is reserved. A version uint32 would have been
load-bearing for nothing — there is no v2 to disambiguate from today, and
adding one speculatively would have been dead weight (a DoS surface, a
constant to maintain, and a check that filtered only random data).

## Key derivation

PBKDF2-HMAC-SHA256, 100,000 iterations, 16-byte salt, 32-byte derived key.

```csharp
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

byte[] key = KeyDerivation.Pbkdf2(
    password: passphrase,
    salt: salt,
    prf: KeyDerivationPrf.HMACSHA256,
    iterationCount: 100_000,
    numBytesRequested: 32);
```

The KDF is provided by the `Microsoft.AspNetCore.Cryptography.KeyDerivation` package (the same one ASP.NET Core Identity uses for password hashing). We use it instead of the BCL's `Rfc2898DeriveBytes` because the BCL's 4-arg constructor (with `HashAlgorithmName`) is not available on `netstandard2.0`; this package fills that gap.

Why PBKDF2 and not Argon2:

- Argon2 is better but requires a NuGet dep. .NET 10 ships PBKDF2 natively. A future migration to Argon2 would ship a new magic byte.
- PBKDF2 at 100k iterations takes ~30ms on modern hardware — fine for a one-time load on session startup.

Why 100k:

- OWASP 2023 recommends 600k for PBKDF2-HMAC-SHA256, but that's for systems that hash very often. We hash once per session load.
- 100k is a balance between security and dev iteration. It's the floor.

## Salt per build, not per file

Wait — let me re-state. The salt is **per encryption call**, not per build. Each call to `Encrypt(plaintext, passphrase)` generates a fresh 16-byte salt and writes it into the header. This means:

- Two builds with the same passphrase produce different ciphertexts (because of fresh salt + IV).
- Rotating the passphrase is straightforward: re-encrypt with the new passphrase.
- The no-encryption path (null/empty passphrase) doesn't write a salt at all.

## Key rotation

Because the salt is in the file, rotating the key is just a re-encrypt with a new passphrase. No format migration needed. The new file gets a fresh salt and IV on top, so the ciphertext changes completely.

## API surface

New / changed public surface in `PlayscriptLoader`:

```csharp
// New primary API
public static byte[] Encrypt(byte[] plaintext, string? passphrase);
public static byte[] Decrypt(byte[] blob, string? passphrase);

// Convenience overloads
public static PlayscriptData LoadData(byte[] blob, string? passphrase);
public static Dictionary<string, ScriptBlock> LoadScripts(byte[] blob, string? passphrase);
public static Dictionary<string, TextBlock> LoadTexts(byte[] blob, string? passphrase);
public static Dictionary<string, ScriptBlock> LoadScripts(string path, string? passphrase);
public static Dictionary<string, TextBlock> LoadTexts(string path, string? passphrase);
public static Dictionary<string, ScriptBlock> LoadScripts(Stream stream, string? passphrase);
public static Dictionary<string, TextBlock> LoadTexts(Stream stream, string? passphrase);
public static PlayscriptData LoadData(string path, string? passphrase);
public static PlayscriptData LoadData(Stream stream, string? passphrase);

// Internal constants
private const byte MagicByte = 0xFE;
private const int SaltSize = 16;
private const int IvSize = 16;
private const int KeySize = 32; // AES-256
private const int HeaderSize = 33;
private const int Pbkdf2Iterations = 100_000;
```

The legacy `AesEncrypt` / `AesDecrypt` methods are removed. This is a breaking change in `EasyPlayscript.Core` — acceptable for a 1.0 release.

## Generator / runtime change

`PlayscriptRuntimeEmitter.Generate(...)` no longer takes an `aesKey` parameter. The generated `PlayscriptRuntime.g.cs` has a constructor that takes the key as a runtime argument:

```csharp
// Key provided at runtime, not embedded in the generated assembly
public PlayscriptRuntimeSession(string aesKey) : this(new PlayscriptRegistry(), aesKey) { }
public PlayscriptRuntimeSession(PlayscriptRegistry registry, string aesKey) : base()
{
    Registry = registry ?? throw new ArgumentNullException(nameof(registry));
    _scripts = new Lazy<Dictionary<string, ScriptBlock>>(
        () => PlayscriptLoader.LoadScripts(ResolvePath("playscripts.bin"), aesKey));
    _texts = new Lazy<Dictionary<string, TextBlock>>(
        () => PlayscriptLoader.LoadTexts(ResolvePath("playscripts.bin"), aesKey));
}
```

The build task still receives `AesKey` via the `PlayscriptAesKey` MSBuild property, used to encrypt. The same key must be passed to the runtime constructor.

`CreateChild()` propagates the key (children share the same encrypted blob + key).

## Backwards compatibility

**None.** Old `.bin` files (pre-magic-byte format) are not loadable by the new code. This is a clean break — appropriate for a 1.0 release.

## What is NOT changing

- The plaintext data format (MessagePack on `PlayscriptData`) is unchanged.
- The `PlayscriptData` schema is unchanged. (Tier 1.7 — a separate schema version field — is a different problem and a different PR.)
- The build task still consumes `PlayscriptAesKey` as a string property.
- The Sample still works, after a small update to pass the key at runtime.

## TDD plan

Tests in `EasyPlayscript.Tests/PlayscriptLoaderTests.cs` (refactored from the original
`PlayscriptSerializationTests.cs`). Order:

1. `Encrypt_Then_Decrypt_RoundTrips_Payload`
2. `Encrypt_Then_Decrypt_RoundTrips_EmptyPayload`
3. `Encrypt_Then_Decrypt_RoundTrips_LargePayload`
4. `Encrypt_Then_Decrypt_RoundTrips_MessagePackPlayscriptData`
5. `Encrypt_Produces_Magic_And_Header` (size floor + magic byte)
6. `Encrypt_Header_Has_Magic_Then_Salt_Then_IV_Then_Ciphertext` (offsets)
7. `Encrypt_Each_Call_Uses_Fresh_Salt_And_IV` (probabilistic — two calls differ)
8. `Encrypted_Header_Does_Not_Leak_Passphrase_In_Plaintext`
9. `Encrypt_Header_Starts_With_Magic_Byte_0xFE` (discriminator)
10. `Decrypt_Of_Raw_Blob_Starting_With_Magic_Byte_Attempts_Decryption` (loud failure)
11. `Decrypt_Of_Real_PlayscriptData_Still_Passes_Through`
12. `Encrypt_With_NullPassphrase_Produces_Unencrypted_Blob`
13. `Encrypt_With_EmptyPassphrase_Produces_Unencrypted_Blob`
14. `Decrypt_Of_Unencrypted_Blob_Returns_Input`
15. `Decrypt_Of_Unencrypted_Blob_With_Null_Passphrase_Returns_Input`
16. `Decrypt_With_Wrong_Passphrase_Throws`
17. `Decrypt_With_Empty_Passphrase_On_Encrypted_Blob_Throws`
18. `Decrypt_Of_Corrupted_Ciphertext_Throws`
19. `Decrypt_Of_Corrupted_Salt_Throws`
20. `Decrypt_Of_Truncated_Header_Throws`
21. `Decrypt_Of_Unknown_Magic_Treats_As_Raw_Passthrough`
22. `Pbkdf2_Iteration_Count_Is_At_Least_Minimum` (security property)
23. `Pbkdf2_Derived_Key_Is_Not_Plain_Sha256_Of_Passphrase`
24. `LoadScripts_From_Bytes_Overload_Respects_Passphrase`
25. `LoadTexts_From_Bytes_Overload_Respects_Passphrase`
26. `LoadScripts_From_Stream_Overload_Respects_Passphrase`
27. `LoadScripts_From_Stream_Null_Passphrase_Works_For_Unencrypted_Blob`
28. `LoadScripts_From_File_Overload_Delegates_To_Stream_Overload`
29. `LoadScripts_From_Bytes_With_Null_Passphrase_Works_For_Unencrypted_Blob`

The legacy `AesEncrypt` / `AesDecrypt` tests are removed (they reference the deleted methods).

## Risk

- Iteration count is hard-coded. If we ever want to bump it, the magic-byte must change too (new format).
- MSBuild task and generator are tightly coupled; any change to the wire format must be done in lockstep with both projects.

## Status

In progress. See git history for incremental commits.
