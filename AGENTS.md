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
```

## Architecture: Two-Pass Parsing

1. **Pass 1 (Structure)**: `PlayscriptStructureHelper` → extracts block types, names, raw content, interface declarations
2. **Pass 2 (Content)**: `PlayscriptContentHelper` → parses script/text content inside `[...]` blocks

ANTLR grammars in `EasyPlayscript.Core/core/playscript/definition/`:
- `PlayscriptStructureLexer.g4` + `PlayscriptStructureParser.g4` (Pass 1)
- `PlayscriptContentLexer.g4` + `PlayscriptContentParser.g4` (Pass 2)

**Position convention**: ANTLR uses 1-based lines, 0-based columns. LSP uses 0-based both.

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

## Key Files

- `EasyPlayscript.Core/Parsing/PlayscriptPipeline.cs` — orchestrates validation
- `EasyPlayscript.Core/Parsing/InterfaceValidator.cs` — cross-file interface validation
- `EasyPlayscript.Generator/PlayscriptGenerator.cs` — main generator entry point
- `EasyPlayscript.Sample/scripts/*.scpt` — example `.scpt` files

## Gotchas

- The `.uid` files are JetBrains Rider cache — ignore them
- `EasyPlayscript.Sample` references `EasyPlayscript.Generator` twice: once as normal dependency, once as `OutputItemType="Analyzer"` for source generation
- `EasyPlayscript.BuildTask` must be built before `EasyPlayscript.Sample` (the sample's MSBuild target references the build task DLL)
- No CI workflows exist — this is a local development repo
