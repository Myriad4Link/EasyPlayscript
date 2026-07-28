# Release Readiness Audit

Generated 2026-07-27 against `main` (pre-1.0). The library is functionally solid — parser, generator, runtime, navigation, async, parent-child sessions, and a basic LSP all work. A 1.0.0 release still has many gaps across packaging, docs, LSP, testing, and CI. Tiers are ordered by severity.

---

## Tier 1 — Release blockers

### 1.1 NuGet package metadata is bare

None of the three `.csproj` files have any of the modern required/expected metadata:

- **Missing:** `<PackageLicenseExpression>MIT</PackageLicenseExpression>` (LICENSE file exists, but no license expression on the package)
- **Missing:** `<PackageProjectUrl>`, `<RepositoryUrl>`, `<RepositoryType>` (no link back to source)
- **Missing:** `<PackageReadmeFile>README.md` (packages currently ship with the stock template `Readme.md` from `EasyPlayscript.Generator` and nothing for Core/BuildTask)
- **Missing:** `<PackageIcon>` (the logo in `assets/` is unused)
- **Missing:** `<PackageTags>` (no discoverability on nuget.org)
- **Missing:** `<Copyright>`, real `<Authors>` (currently just `"EasyPlayscript"`)
- **Missing:** `<PackageReleaseNotes>`, no `CHANGELOG.md`
- **Missing:** `README.md` is in the root but not wired into any project for packaging

### 1.2 "Early development" badge still on README

`README.md:17` still says `https://img.shields.io/badge/status-early_development-F44336` while the package version is `1.0.0`. The intro paragraph also says "Still in early development." Both contradict a 1.0 release.

### 1.3 CHANGELOG / version history

No `CHANGELOG.md` anywhere. Required for any release — users need to know what changed.

### 1.4 No CI/CD

No `.github/workflows/`, no Azure DevOps, nothing. `dotnet test` is manual via `coverage.ps1`/`pack-local.ps1`. A release today has zero automated gate (no PR builds, no package publishing).

### 1.5 LSP doesn't actually show sema errors

`EasyPlayscript.LSP/Parsing/PlayscriptDocumentParser.cs:10` has a literal `// TODO: add errors publishing other than syntax and lexer ones.` The LSP currently only surfaces ANTLR syntax/lexer errors. It does **not** report:

- SCPT005 (undeclared consumer call)
- SCPT007 / SCPT008 (argument type/count mismatch)
- SCPT004 (duplicate names)
- SCPT009 (missing implementation)

For a 1.0 with an LSP, the whole point is to catch these in the editor. Right now the editor only catches "this bracket is wrong" — the unique value of the LSP is missing.

### 1.6 `AesKey` is a string, no KDF

`PlayscriptLoader.cs:69` derived the key with a single SHA-256 over the user-supplied string. No iteration count, no salt. For a feature marketed as "optional AES encryption to protect script content," this was borderline broken cryptography. **Status:** fixed. The loader now uses `KeyDerivation.Pbkdf2` from the `Microsoft.AspNetCore.Cryptography.KeyDerivation` package (the same KDF ASP.NET Core Identity uses) with 100,000 iterations, a per-blob 16-byte salt, and a 32-byte AES-256 key. See `docs/CRYPTOGRAPHY.md`.

### 1.7 No wire-format version on `PlayscriptData`

`EasyPlayscript.Core/DataModel/PlayscriptData.cs` has no version key. Once users ship a build with a serialized blob, any future schema change is a breaking migration. There is no upgrade path or even a magic number to fail on version mismatch. **Status:** partially addressed — the *encryption wrapper* has a single magic byte (`0xFE`) that distinguishes "encrypted" from "raw" but does not carry an in-format version field. Future encryption migrations would use a new magic byte. The underlying `PlayscriptData` schema version remains a separate problem and a separate PR.

### 1.8 Key embedded in generated assembly

`PlayscriptRuntimeEmitter.cs:70` embeds the AES key as a plain string literal in the generated `PlayscriptRuntime.g.cs`. Anyone with a free .NET decompiler reads the key in seconds. The "encryption" is purely cosmetic for any shipped game. **Status:** being fixed alongside 1.6 — the key is now a runtime input.

---

## Tier 2 — Strongly recommended

### 2.1 LSP missing core language features

The LSP only has sync + semantic tokens. For a "playable" editor experience, the following are expected:

- **Hover** — show interface signature on `@callName`
- **Completion** — `@<TAB>` suggests declared interfaces; signature help inside `(`
- **Go-to-Definition** — `@callName` jumps to the `interface` declaration
- **References** — every `@callName` is a reference
- **Document Symbols** — outline of interfaces/scripts/texts
- **Workspace Symbols** — `Ctrl+T` to jump to any block by name
- **Rename** — rename an interface and update all call sites
- **Document Formatting** — basic indentation/trimming
- **Code Actions** — "create [Implementation] stub" on an interface

### 2.2 No `Directory.Build.props`

Common properties (`LangVersion`, `Nullable`, `Authors`, `Company`, `NeutralLanguage`, copyright) are repeated/inconsistent across projects. Centralizing would also let you bump versions in one place.

### 2.3 Determinism / source link

- No `<PublishRepositoryUrl>` / `<EmbedUntrackedSources>` / `<Deterministic>` setup. NuGet packages will have non-reproducible builds and no source link.
- No `<IncludeSymbols>` / `<SymbolPackageFormat>` (no `.snupkg`).

### 2.4 Test coverage is uneven

- `PlayscriptBuildTask` (the MSBuild task) has **zero** tests. Only the Sample uses it.
- No end-to-end test that exercises the full pipeline: write `.scpt` → MSBuild task → `PlayscriptLoader.LoadScripts` → `session.GetScript(...).Run()`.
- `PlayscriptResult` (in `Parsing/PlayscriptResult.cs`) is dead code that asserts `Scripts` only — never used anywhere.
- No benchmark project (BenchmarkDotNet). For a "DSL focused on narrative flow in games," latency is a real concern.
- LSP integration tests cover sync + edit, but no round-trip test that proves the LSP can serve multiple documents concurrently.
- No tests for `PlayscriptSessionScope` resolution in deeply nested children (Sample exercises it manually, not in a test).
- `PlayscriptSerializationTests` only covers the legacy `AesEncrypt`/`AesDecrypt` path; after the new cryptography work it should also cover versioned blobs, salt rotation, KDF parameter selection, and corrupt-blob handling.

### 2.5 Documentation

- `EasyPlayscript.Generator/Readme.md` is the **stock template from `dotnet/roslyn`** ("Roslyn Source Generators Sample"). Never customized. Packaged users will see this.
- No `CONTRIBUTING.md`, no `CODE_OF_CONDUCT.md`, no `SECURITY.md`.
- No "Installation" / "Getting Started" standalone doc — README is the only entry point.
- No "MSBuild integration" doc — the build task is mentioned in passing.
- No "Upgrading / Breaking Changes" doc.
- No `.scpt` syntax reference as a separate file.
- No editor setup docs for VSCode / Rider / Neovim.

### 2.6 Sample is a demo, not documentation

`EasyPlayscript.Sample` has 4 toy `.scpt` files and a 182-line `Program.cs` that demos 10 different things. It should have:

- A realistic multi-page example (something like a Macbeth scene — currently the README has the snippet but the sample has Chinese placeholder text).
- Realistic games-services (`AudioSystem`, `UiSystem`, `DataSystem` are placeholders).
- A README explaining what each example demonstrates.
- A `keyword_test.scpt` that actually exercises every C# keyword as an identifier (e.g., `script class[`, `text void[`, `interface string(...)`).
- The `PlayscriptAesKey` is hardcoded to `dev-key-change-me` in the sample — should be loaded from environment / user secret for a non-dev sample.

### 2.7 Generated APIs throw opaque errors

When called without a session, `Script.Run()`, `Script.RenderNextLine()`, `Text.Render()` all throw `InvalidOperationException` with a long pre-formatted message. This is a public API — the errors should probably be a documented `InvalidOperationException` (or better, a custom `PlayscriptException` type) with a clear property bag.

### 2.8 `PlayscriptGenerator.HasErrors` is set inconsistently

`PlayscriptGenerator.cs:70-76` calls `PlayscriptPipeline.Validate` but **does not** flag `ctx.Data.HasErrors` for the cross-file `Validate()` result, even though the same pipeline does flag it in the per-file loop. The generated code is still suppressed correctly via the per-file errors, but if a project has *only* cross-file errors (e.g., SCPT005 from a script calling an interface declared in another file), the generator may emit broken code.

This is a subtle correctness bug.

### 2.9 `PlayscriptLoader` has no Stream / byte[] / embedded-resource overload

Only `LoadScripts(string path, string key)` exists. There's no `LoadScripts(byte[] data, string key)` or `LoadScripts(Stream s, string key)`. Any consumer who wants to ship the `.bin` as an embedded resource has to round-trip through disk. **Status:** being fixed alongside 1.6.

### 2.10 `PlayscriptCompilationData.HasErrors` semantics differ between modes

The `BuildTask` (`PlayscriptBuildTask.cs`) only logs errors but doesn't set `HasErrors` from the cross-file validation step. The Roslyn source generator does, partially (see 2.8). A user running the build task outside of a Roslyn context can't tell from the data whether to abort — they have to inspect diagnostics.

---

## Tier 3 — Polish / nice-to-have

- **Naming consistency**: `ScriptKey`/`TextKey` (plural form, enums) vs the docs which show `PlayscriptRuntimeSession.ScriptKey.act_i_scene_i` (using the enum value as a key). It's idiomatic but a "new to this" user will trip on it.
- **Decimals as `double` in generated code**: `DecimalArgument` becomes `(double)` in the generator. For an RPG-friendly DSL, this is lossy. There's no `decimal` type in the C# type system used here.
- **No `try`/cancellation behavior in `RenderNext*Async`**: If a registered service throws, the navigator state is left half-advanced; no `try/finally` to keep the pointer consistent.
- **`ScriptNavigator` is mutable state on an immutable `ScriptBlock`**. There's no way to clone or fork the navigator (e.g., to peek at a future page without advancing).
- **No "save state" / persistence helpers** for `ScriptPointer`. Users can do `script.Pointer`, but there's no built-in serialization to/from JSON for save games.
- **`Script.g.cs` and `Text.g.cs` always generated**, even with zero `.scpt` files. They reference `PlayscriptRegistry`, which is also always generated. Works, but bloats compile.
- **The `PlayscriptGenerator` is split across two `IIncrementalGenerator`s** (`PlayscriptGenerator` and `ScriptRegistry`). The split is leaky: `Script.g.cs` references `PlayscriptRuntimeSession` by string name only.
- **`SCPT001` is skipped** (codes go 002, 003, ...). Either intentional or a typo from the original spec; should be confirmed.
- **ANTLR runtime is bundled in `EasyPlayscript.Core`** (it has to be for the source generator, but it doesn't have to be for the runtime). For consumers of `EasyPlayscript.Core` who don't use the source generator, the ANTLR runtime is dead weight.
- **No benchmarks** for: parsing, registry dispatch, navigation, MessagePack deserialization.
- **No `analyze` rule for editor performance**: `IMetricsLogger` / `RegisterPostInitializationOutput` not used; could be optimized.
- **No .vscode/launch.json / .vscode/tasks.json** for development.
- **No editorconfig** — formatting is whatever the IDE defaults to.

---

## Tier 4 — What I'd actually block a release on

| # | Item | Why |
|---|------|-----|
| 1 | NuGet package metadata (license, repo URL, tags, readme, icon) | NuGet.org will reject / demote the package |
| 2 | Remove "early development" badge from README | Marketing lie on release day |
| 3 | Add a `CHANGELOG.md` | Required for users |
| 4 | Make the LSP report SCPT005/007/008/004/009 | The headline feature is the editor experience; right now it's just syntax highlighting |
| 5 | Fix the `HasErrors` inconsistency in `PlayscriptGenerator` (2.8) | A real correctness bug |
| 6 | Add a wire-format version to `PlayscriptData` (in-progress via the cryptography refactor) | Required for any future migration story |
| 7 | Set up at least GitHub Actions build+test | A "release" with no CI is a footgun |
| 8 | Replace the stock template `Readme.md` in `EasyPlayscript.Generator` | Embarrassing to ship |
| 9 | Decide: is the AES story a security claim or a speedbump? (in-progress) | Either implement real KDF+versioning or rename the feature to "obfuscation" |

After that, Tier 2 features (Hover/Completion/Definition in LSP, `Directory.Build.props`, source link, full pipeline tests, docs) are the difference between "released" and "ready for adoption."
