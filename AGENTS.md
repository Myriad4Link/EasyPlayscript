# EasyPlayscript — Agent Guide

## What This Is

A custom scripting language (`.scpt` files) with a two-pass ANTLR parser, Roslyn source generator, and MSBuild integration. The generator produces `PlayscriptRegistry.g.cs`, `PlayscriptContext.g.cs`, `PlayscriptSession.g.cs`, `Script.g.cs`, and `Text.g.cs` at compile time.

## Project Structure

| Project | Target | Role |
|---------|--------|------|
| `EasyPlayscript.Core` | netstandard2.0 | ANTLR parsers, data models, validation |
| `EasyPlayscript.Generator` | netstandard2.0 | Roslyn `IIncrementalGenerator` |
| `EasyPlayscript.BuildTask` | netstandard2.0 | MSBuild task for binary compilation |
| `EasyPlayscript.Tests` | net9.0 | xUnit tests |
| `EasyPlayscript.Sample` | net9.0 | Demo app with `.scpt` files in `scripts/` |

**Key**: `EasyPlayscript.Core` has `RootNamespace` = `EasyPlayscript` (not `EasyPlayscript.Core`).

## Commands

```bash
dotnet build                    # Build entire solution
dotnet test                     # Run all tests (xUnit)
dotnet test --filter "PlayscriptGeneratorTests"  # Run specific test class
dotnet run --project EasyPlayscript.Sample        # Run sample app
./pack-local.ps1                # Rebuild & repack all NuGet packages into nuget-local/
```

**SDK**: .NET 10.0.301 required (`global.json` with `rollForward: latestMinor`).

**NuGet lock issue**: The LSP server may lock DLLs in the global NuGet cache. If `dotnet restore` fails with "Access to the path ... is denied", use `dotnet build --no-restore`.

## Architecture: Two-Pass Parsing

1. **Pass 1 (Structure)**: `PlayscriptStructureHelper` → extracts block types, names, raw content, interface declarations
2. **Pass 2 (Content)**: `PlayscriptContentHelper` → parses script/text content inside `[...]` blocks

ANTLR grammars in `EasyPlayscript.Core/core/playscript/definition/`:
- `PlayscriptStructureLexer.g4` + `PlayscriptStructureParser.g4` (Pass 1)
- `PlayscriptContentLexer.g4` + `PlayscriptContentParser.g4` (Pass 2)

**Position convention**: ANTLR uses 1-based lines, 0-based columns. LSP uses 0-based both.

## PlayscriptSession: The User-Facing API

`PlayscriptSession` (generated, inherits `PlayscriptContext`) is the primary entry point. It encapsulates registry, script/text data, and scene context in one object.

```csharp
var session = new PlayscriptSession();
session.Register(new AudioSystem(), ActionScope.GlobalService);
session.Register(new UiSystem(), ActionScope.TransientNode);

session.GetText(key).Render();      // fluent chain — no extra params
session.GetScript(key).Run();       // dispatches all consumer calls
session.DispatchCall(call);         // low-level single call dispatch
```

### ActionScope Dispatch

- **GlobalService**: Stored in a `Dictionary<Type, object>` (`_globals`). Dispatched via `_globals.TryGetValue(typeof(T))`.
- **TransientNode**: Stored in `TransientNodeContext` (`SceneContext`). Dispatched via `context.Get<T>()`.

`TransientNodeContext` (`EasyPlayscript.Core/TransientNodeContext.cs`) is a type-keyed dictionary with `Bind<T>()` and `Get<T>()`.

### Generated Files

| File | Generator | Contents |
|------|-----------|----------|
| `PlayscriptRegistry.g.cs` | `PlayscriptRegistryEmitter` | `_globals` dict, `RegisterGlobal<T>()`, `DispatchCall()` |
| `PlayscriptContext.g.cs` | `PlayscriptContextEmitter` | `ScriptKey`/`TextKey` enums, `GetScript()`/`GetText()`, lazy loader |
| `PlayscriptSession.g.cs` | `PlayscriptSessionEmitter` | `PlayscriptSession` subclass with `Register<T>()`, `DispatchCall()` |
| `Script.g.cs` | `ScriptRegistry` | `Script` class with `Run()` (session-aware) |
| `Text.g.cs` | `ScriptRegistry` | `Text` class with `Render()` (session-aware + original overloads) |

`Script.Run()` and `Text.Render()` (parameterless) throw if `Session` is null — they only work when created via `session.GetScript()`/`session.GetText()`.

`PlayscriptContext` is **not sealed** (allows `PlayscriptSession` to inherit).

## Diagnostic Codes

| Code | Meaning |
|------|---------|
| SCPT002 | Lexer error (unexpected token) |
| SCPT003 | Parser error (mismatched input) |
| SCPT004 | Duplicate script/text name |
| SCPT005 | Undeclared consumer call (`@foo()` with no interface) |
| SCPT006 | Duplicate interface signature |
| SCPT007 | Argument type mismatch |
| SCPT008 | Argument count mismatch |
| SCPT009 | Missing `[Implementation]` method |
| SCPT010 | Duplicate `[Implementation]` |
| SCPT011 | Unused `[Implementation]` (warning) |

All codes defined in `EasyPlayscript.Generator/PlayscriptDiagnostics.cs`.

## Generator Testing Pattern

Tests in `EasyPlayscript.Tests/` use `CSharpGeneratorDriver` with:
- `TestAdditionalFile` (from `Utils/`) to simulate `.scpt` files
- `TestAnalyzerConfigOptionsProvider` for build properties (`PlayscriptOutputPath`, `PlayscriptAesKey`)

Pattern: create generator → add additional files → run driver → assert on generated syntax tree or diagnostics.

Emitter tests (`PlayscriptRegistryEmitterTests`, `PlayscriptContextEmitterTests`, `PlayscriptSessionEmitterTests`) call emitters directly with hand-built `PlayscriptCompilationData` — no Roslyn driver needed.

`ScriptRegistryTests` uses `CSharpGeneratorDriver` with the `ScriptRegistry` generator (post-initialization, no .scpt files needed).

## Key Files

- `EasyPlayscript.Core/Parsing/PlayscriptPipeline.cs` — orchestrates validation
- `EasyPlayscript.Core/Parsing/InterfaceValidator.cs` — cross-file interface validation
- `EasyPlayscript.Core/Parsing/ImplementationValidator.cs` — validates `[Implementation]` method presence and duplicates
- `EasyPlayscript.Generator/PlayscriptGenerator.cs` — main generator entry point, emits all `.g.cs` files
- `EasyPlayscript.Generator/PlayscriptRegistryEmitter.cs` — generates `PlayscriptRegistry.g.cs` with `_globals` dict + scope-aware dispatch
- `EasyPlayscript.Generator/PlayscriptContextEmitter.cs` — generates `PlayscriptContext.g.cs` (non-sealed, with enums)
- `EasyPlayscript.Generator/PlayscriptSessionEmitter.cs` — generates `PlayscriptSession.g.cs`
- `EasyPlayscript.Generator/ScriptRegistry.cs` — generates `Script.g.cs` and `Text.g.cs` (post-initialization)
- `EasyPlayscript.Core/TransientNodeContext.cs` — transient node type-map for scene-scoped components
- `EasyPlayscript.Core/ImplementationAttribute.cs` — `[Implementation]` attribute + `ActionScope` enum
- `EasyPlayscript.Sample/scripts/*.scpt` — example `.scpt` files
- `LSP-PLAN.md` — in-progress plan for an LSP server (not yet implemented)

## Gotchas

- The `.uid` files are JetBrains Rider cache — ignore them
- `EasyPlayscript.Sample` references NuGet packages (not project references). After changing Core or Generator, run `./pack-local.ps1` before building the Sample
- `EasyPlayscript.BuildTask` must be built before `EasyPlayscript.Sample` (the sample's MSBuild target references the build task DLL)
- `nuget-local/` is the local NuGet feed; `pack-local.ps1` rebuilds packages there and clears global cache
- `NuGet.Config` clears default sources and adds only `nuget.org` + `./nuget-local`
- No CI workflows exist — this is a local development repo
- `Script.g.cs` and `Text.g.cs` are emitted via `RegisterPostInitializationOutput` (runs before other generators). They reference `PlayscriptSession` by name, which is generated later. This works because all generated sources compile together
- `PlayscriptRegistry.DispatchCall` switch cases use `{ }` blocks to scope local variables — C# switch cases share scope without blocks
