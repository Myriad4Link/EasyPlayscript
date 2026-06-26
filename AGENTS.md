# EasyPlayscript — Agent Guide

## What This Is

A custom scripting language (`.scpt` files) with a two-pass ANTLR parser, Roslyn source generator, and MSBuild integration. The generator produces `PlayscriptRegistry.g.cs` and `PlayscriptContext.g.cs` at compile time.

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

## Architecture: Two-Pass Parsing

1. **Pass 1 (Structure)**: `PlayscriptStructureHelper` → extracts block types, names, raw content, interface declarations
2. **Pass 2 (Content)**: `PlayscriptContentHelper` → parses script/text content inside `[...]` blocks

ANTLR grammars in `EasyPlayscript.Core/core/playscript/definition/`:
- `PlayscriptStructureLexer.g4` + `PlayscriptStructureParser.g4` (Pass 1)
- `PlayscriptContentLexer.g4` + `PlayscriptContentParser.g4` (Pass 2)

**Position convention**: ANTLR uses 1-based lines, 0-based columns. LSP uses 0-based both.

## ActionScope: Global vs Transient Dispatch

`[Implementation]` has a `Scope` property (`ActionScope.GlobalService` by default, or `ActionScope.TransientNode`).

- **GlobalService**: The generated registry stores a field and `Register()` method for the class. Dispatch calls the field directly.
- **TransientNode**: No field or `Register()` method is generated. Dispatch fetches the instance from `PlayscriptExecutionContext.Get<T>()` at runtime.

A single class can have mixed scopes — the registry will have a field for it (if any method is GlobalService), and transient methods route through the context.

`PlayscriptExecutionContext` (`EasyPlayscript.Core/PlayscriptExecutionContext.cs`) is a type-keyed dictionary:
- `Bind<T>(instance)` — register a transient node
- `Get<T>()` — retrieve it (returns null if unbound)

`DispatchCall` signature is `DispatchCall(ConsumerCallItem call, PlayscriptExecutionContext context)`. All callers must pass context.

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

Emitter tests (`PlayscriptRegistryEmitterTests`) call `PlayscriptRegistryEmitter.Generate()` directly with hand-built `PlayscriptCompilationData` — no Roslyn driver needed.

## Key Files

- `EasyPlayscript.Core/Parsing/PlayscriptPipeline.cs` — orchestrates validation
- `EasyPlayscript.Core/Parsing/InterfaceValidator.cs` — cross-file interface validation
- `EasyPlayscript.Core/Parsing/ImplementationValidator.cs` — validates `[Implementation]` method presence and duplicates
- `EasyPlayscript.Generator/PlayscriptGenerator.cs` — main generator entry point
- `EasyPlayscript.Generator/PlayscriptRegistryEmitter.cs` — generates `PlayscriptRegistry.g.cs` with scope-aware dispatch
- `EasyPlayscript.Generator/ScriptRegistry.cs` — generates `Script` and `Text` data classes
- `EasyPlayscript.Core/PlayscriptExecutionContext.cs` — transient node type-map for scene-scoped components
- `EasyPlayscript.Sample/scripts/*.scpt` — example `.scpt` files
- `LSP-PLAN.md` — in-progress plan for an LSP server (not yet implemented)

## Gotchas

- The `.uid` files are JetBrains Rider cache — ignore them
- `EasyPlayscript.Sample` references NuGet packages (not project references). After changing Core or Generator, run `./pack-local.ps1` before building the Sample
- `EasyPlayscript.BuildTask` must be built before `EasyPlayscript.Sample` (the sample's MSBuild target references the build task DLL)
- `nuget-local/` is the local NuGet feed; `pack-local.ps1` rebuilds packages there and clears global cache
- `NuGet.Config` clears default sources and adds only `nuget.org` + `./nuget-local`
- No CI workflows exist — this is a local development repo
- `Text.Render(PlayscriptRegistry registry)` creates an empty `PlayscriptExecutionContext` internally — transient-only classes won't be resolved through this overload
