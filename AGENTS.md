# EasyPlayscript — Agent Guide

## What This Is

A custom scripting language (`.scpt` files) with a two-pass ANTLR parser, Roslyn source generator, and MSBuild integration. The generator produces `PlayscriptRegistry.g.cs`, `PlayscriptRuntime.g.cs`, `Script.g.cs`, and `Text.g.cs` at compile time.

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

## PlayscriptRuntimeSession: The User-Facing API

`PlayscriptRuntimeSession` (generated, extends `PlayscriptSessionScope`) is the primary entry point. It encapsulates services, parent chain, registry, and script/text data.

```csharp
var session = new PlayscriptRuntimeSession();
session.Register(new AudioSystem());
session.Register(new UiSystem());

session.GetText(key).Render();      // fluent chain — no extra params
session.GetScript(key).Run();       // dispatches all consumer calls
session.DispatchCall(call);         // low-level single call dispatch

// Parent-child: child inherits parent services, can override
var child = session.CreateChild();
child.Register(new CombatAudio());  // shadows parent's AudioSystem
child.GetScript(key).Run();         // uses child's service chain
```

### Script Navigation API

The generated `Script` class supports pointer-based step-by-step navigation. Navigation logic lives in `ScriptNavigator` (Core); the generated class delegates to it.

```csharp
var script = session.GetScript(key);

script.Pointer                        // ScriptPointer(0,0,0)
script.RenderNextLine()               // string? — dispatches consumer calls + returns text
script.RenderNextParagraph()          // string? — lines joined by newline
script.RenderNextPage()               // string? — paragraphs joined by blank line
script.IsLastLineOfParagraph          // bool (also: IsLastLineOfPage, IsLastLineOfScript, etc.)
script.JumpTo(pointer)                // void — validates bounds
script.Reset()                        // void — rewinds to (0,0,0)
```

- `Render*` methods return `null` when the pointer is past the end.
- `IsLast*` properties return `true` for empty scripts and when the pointer is past the end.
- `Run()` is unaffected by the pointer — it always dispatches everything.

### Service Dispatch (Parent-Child Chain)

All services are stored in a `ConcurrentDictionary<Type, object>` on `PlayscriptSessionScope`. Dispatch walks the parent chain: child-local → parent → grandparent → ...

```csharp
session.Register(new AudioSystem());       // stored locally
child.Get<AudioSystem>();                  // checks child._services, then parent._services, etc.
```

The generated `PlayscriptRegistry.DispatchCall(call, session)` calls `session.Get<T>()` for every consumer call. There is no separate `ActionScope` or `TransientNodeContext` — the parent-child chain replaces both.

### Generated Files

| File | Generator | Contents |
|------|-----------|----------|
| `PlayscriptRegistry.g.cs` | `PlayscriptRegistryEmitter` | `DispatchCall()` switch using `session.Get<T>()` |
| `PlayscriptRuntime.g.cs` | `PlayscriptRuntimeEmitter` | `PlayscriptRuntimeSession` class (extends `PlayscriptSessionScope`), `Registry`, `CreateChild()`, enums, lazy loader |
| `Script.g.cs` | `ScriptRegistry` | `Script` class with `Run()` + pointer-based navigation (session-aware) |
| `Text.g.cs` | `ScriptRegistry` | `Text` class with `Render()` (session-aware) |

`Script.Run()` and `Text.Render()` (parameterless) throw if `Runtime` is null — they only work when created via `session.GetScript()`/`session.GetText()`.

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

Emitter tests (`PlayscriptRegistryEmitterTests`, `PlayscriptRuntimeEmitterTests`) call emitters directly with hand-built `PlayscriptCompilationData` — no Roslyn driver needed.

`ScriptRegistryTests` uses `CSharpGeneratorDriver` with the `ScriptRegistry` generator (post-initialization, no .scpt files needed).

## Key Files

- `EasyPlayscript.Core/Parsing/PlayscriptPipeline.cs` — orchestrates validation
- `EasyPlayscript.Core/Parsing/InterfaceValidator.cs` — cross-file interface validation
- `EasyPlayscript.Core/Parsing/ImplementationValidator.cs` — validates `[Implementation]` method presence and duplicates
- `EasyPlayscript.Generator/PlayscriptGenerator.cs` — main generator entry point, emits all `.g.cs` files
- `EasyPlayscript.Generator/PlayscriptRegistryEmitter.cs` — generates `PlayscriptRegistry.g.cs` with `DispatchCall()` using `session.Get<T>()`
- `EasyPlayscript.Generator/PlayscriptRuntimeEmitter.cs` — generates `PlayscriptRuntime.g.cs` (`PlayscriptRuntimeSession` class extending `PlayscriptSessionScope`)
- `EasyPlayscript.Generator/ScriptRegistry.cs` — generates `Script.g.cs` and `Text.g.cs` (post-initialization)
- `EasyPlayscript.Core/PlayscriptSessionScope.cs` — base class with `ConcurrentDictionary` services, parent chain, `Register<T>`, `Get<T>`, `CreateChild`
- `EasyPlayscript.Core/ScriptNavigator.cs` — pointer-based navigation for Script (RenderNext*, IsLast*, JumpTo, Reset)
- `EasyPlayscript.Core/ScriptPointer.cs` — immutable value type for script position (pageIndex, paragraphIndex, lineIndex)
- `EasyPlayscript.Core/ImplementationAttribute.cs` — `[Implementation]` attribute (no scope — all services use parent-child chain)
- `EasyPlayscript.Sample/scripts/*.scpt` — example `.scpt` files
- `LSP-PLAN.md` — in-progress plan for an LSP server (not yet implemented)

## Gotchas

- The `.uid` files are JetBrains Rider cache — ignore them
- `EasyPlayscript.Sample` references NuGet packages (not project references). After changing Core or Generator, run `./pack-local.ps1` before building the Sample
- `EasyPlayscript.BuildTask` must be built before `EasyPlayscript.Sample` (the sample's MSBuild target references the build task DLL)
- `nuget-local/` is the local NuGet feed; `pack-local.ps1` rebuilds packages there and clears global cache
- `NuGet.Config` clears default sources and adds only `nuget.org` + `./nuget-local`
- No CI workflows exist — this is a local development repo
- `Script.g.cs` and `Text.g.cs` are emitted via `RegisterPostInitializationOutput` (runs before other generators). They reference `PlayscriptRuntimeSession` by name, which is generated later. This works because all generated sources compile together
- `PlayscriptRegistry.DispatchCall` switch cases use `{ }` blocks to scope local variables — C# switch cases share scope without blocks
- `ScriptNavigator` (Core) owns all pointer state; the generated `Script` class delegates to it. The navigator takes a `Func<Line, string>` render callback so it can be tested without a runtime. The generated Script passes its own `RenderLine` method (which dispatches consumer calls) as that callback
- The generated `PlayscriptRuntimeSession` inherits from `PlayscriptSessionScope` (Core). The base holds the service dictionary and parent chain; the generated class adds `Registry`, `DispatchCall`, `CreateChild` override, and script/text loading
- `CreateChild()` returns `PlayscriptRuntimeSession` (covariant return). The child shares the same `Registry` instance as the parent
