# EasyPlayscript — Agent Guide

## What This Is

A custom scripting language (`.scpt` files) with a two-pass ANTLR parser, Roslyn source generator, and MSBuild integration. The generator produces `PlayscriptRegistry.g.cs`, `PlayscriptRuntime.g.cs`, `Script.g.cs`, and `Text.g.cs` at compile time. An LSP server provides editor support.

## Project Structure

| Project | Target | Role |
|---------|--------|------|
| `EasyPlayscript.Core` | netstandard2.0 | ANTLR parsers, data models, validation |
| `EasyPlayscript.Generator` | netstandard2.0 | Roslyn `IIncrementalGenerator` |
| `EasyPlayscript.BuildTask` | netstandard2.0 | MSBuild task for binary compilation |
| `EasyPlayscript.LSP` | net10.0 | LSP server (OmniSharp), references Core |
| `EasyPlayscript.Tests` | net9.0 | xUnit tests for Core + Generator |
| `EasyPlayscript.LSP.Tests` | net10.0 | xUnit tests for LSP |
| `EasyPlayscript.Sample` | net9.0 | Demo app with `.scpt` files in `scripts/` |

**Key**: `EasyPlayscript.Core` has `RootNamespace` = `EasyPlayscript` (not `EasyPlayscript.Core`).

## Commands

```bash
dotnet build                              # Build entire solution
dotnet test                               # Run all tests (xUnit)
dotnet test --filter "PlayscriptGeneratorTests"  # Run specific test class
dotnet test EasyPlayscript.Tests          # Run only Core/Generator tests
dotnet test EasyPlayscript.LSP.Tests      # Run only LSP tests
dotnet run --project EasyPlayscript.Sample       # Run sample app
./pack-local.ps1                          # Rebuild & repack NuGet packages into nuget-local/
```

**Note**: `dotnet test` takes the project path as a positional argument, not `--project`. Use `dotnet test EasyPlayscript.LSP.Tests`, not `dotnet test --project EasyPlayscript.LSP.Tests`.

**SDK**: .NET 10.0.301 required (`global.json` with `rollForward: latestMinor`).

**NuGet lock issue**: The LSP server may lock DLLs in the global NuGet cache. If `dotnet restore` fails with "Access to the path ... is denied", use `dotnet build --no-restore`.

## Architecture: Two-Pass Parsing

1. **Pass 1 (Structure)**: `PlayscriptStructureHelper` → extracts block types, names, raw content, interface declarations
2. **Pass 2 (Content)**: `PlayscriptContentHelper` → parses script/text content inside `[...]` blocks

ANTLR grammars in `EasyPlayscript.Core/core/playscript/definition/`:
- `PlayscriptStructureLexer.g4` + `PlayscriptStructureParser.g4` (Pass 1)
- `PlayscriptContentLexer.g4` + `PlayscriptContentParser.g4` (Pass 2)

**Regenerating ANTLR**: After editing `.g4` files, regenerate with:
```bash
java -jar antlr-4.13.2-complete.jar -Dlanguage=CSharp -visitor -no-listener <grammar.g4> -o EasyPlayscript.Core/Parsing/Visitor/Content
```
Run for both `PlayscriptContentLexer.g4` and `PlayscriptContentParser.g4`. Do the same for Structure grammars with `-o EasyPlayscript.Core/Parsing/Visitor/Structure`.

- **No ANTLR tool is bundled** — download `antlr-4.13.2-complete.jar` from antlr.org. Match the version in generated file headers (currently 4.13.2).
- **Never use `-package` flag** — the grammars use `@header { namespace ...; }` for file-scoped namespaces. `-package` adds a conflicting block namespace.
- Java is required (`java -version` must work).

**Position convention**: ANTLR uses 1-based lines, 0-based columns. LSP uses 0-based both.

## Content Syntax (inside `[...]` blocks)

**Script blocks**: lines separated by newlines; blank lines separate paragraphs; `/` on its own line separates pages.

**Line segments**: `+` is an inline delimiter that splits a line into segments. Use `RenderNextLineSegment()` to iterate segment-by-segment; `RenderNextLine()` concatenates all segments.
```
script name [
    Hello, +World!        # 2 segments: "Hello, " and "World!"
    Goodbye, +Cruel World # 2 segments: "Goodbye, " and "Cruel World"
]
```

**Escape characters**: `\@`, `\#`, `\/`, `\\`, `\"`, `\n`, `\+`. The `+` must be escaped as `\+` when used literally in a segment (since unescaped `+` is the segment delimiter). Unescape logic is in `PlayscriptCodeBuilder.Unescape()`.

**Text blocks**: same syntax but `/` is literal content (not a page break), and `+` has no special meaning (not a segment delimiter).

## Async Interfaces

Grammar supports `async` keyword on interface declarations:
```
async interface fetch_user_name(user_id: int) : string
async interface log_event(event: string) : void
```

Rules:
- `async interface` requires `[Implementation]` methods to return `Task<T>` (or `Task` for void) and be `async`
- Sync `interface` requires sync implementations — mixing is an error (SCPT012/SCPT013)
- Sync rendering (`Run()`, `RenderNextLine()`) fire-and-forgets async calls (`_ = impl.Method(args)`) — return values are lost
- Async rendering (`RunAsync()`, `RenderNextLineAsync()`) properly awaits all calls
- `ImplementationScanner` detects async via `INamedTypeSymbol.OriginalDefinition` checking for `System.Threading.Tasks.Task` / `Task<T>`

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
script.RenderNextLineSegment()        // SegmentRenderResult? — text + segment flags + pointer, or null at end
script.RenderNextLine()               // LineRenderResult? — text + all 6 flags + pointer, or null at end
script.RenderNextParagraph()          // ParagraphRenderResult? — text + paragraph/page flags
script.RenderNextPage()               // PageRenderResult? — text + IsLastPage flag only
script.IsLastLineOfParagraph          // bool (also: IsLastLineOfPage, IsLastLineOfScript, etc.)
script.JumpTo(pointer)                // void — validates bounds
script.Reset()                        // void — rewinds to (0,0,0)
```

- `Render*` methods return sealed subtypes of `RenderResult` — `null` when the pointer is past the end:
  - `SegmentRenderResult`: `IsLastSegmentOfLine`, `IsLastSegmentOfParagraph`, `IsLastSegmentOfPage`, `IsLastSegmentOfScript`
  - `LineRenderResult`: all 6 flags (`IsLastLineOfParagraph`, `IsLastLineOfPage`, `IsLastLineOfScript`, `IsLastParagraphOfPage`, `IsLastParagraphOfScript`, `IsLastPage`)
  - `ParagraphRenderResult`: `IsLastParagraphOfPage`, `IsLastParagraphOfScript`, `IsLastPage`
  - `PageRenderResult`: only `IsLastPage`
- Base class `RenderResult` (abstract) has `Text`, `Pointer`, `IsLastPage` — shared across all subtypes
- Use `is LineRenderResult` / `is ParagraphRenderResult` pattern matching to access subtype-specific flags
- Flags are captured **before** the pointer advances (they describe the rendered unit, not the next position)
- `IsLast*` properties on `Script` reflect live navigator state (post-advance); use `RenderResult` flags for pre-advance state
- `Run()` is unaffected by the pointer — it always dispatches everything
- Async variants: `RenderNextLineSegmentAsync()`, `RenderNextLineAsync()`, `RenderNextParagraphAsync()`, `RenderNextPageAsync()`, `RunAsync()` — properly await async implementations
- `Text` has `RenderAsync()` overloads mirroring sync `Render()`

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
| `PlayscriptRegistry.g.cs` | `PlayscriptRegistryEmitter` | `DispatchCall()` switch using `session.Get<T>()`; `DispatchCallAsync()` with `await` for async impls |
| `PlayscriptRuntime.g.cs` | `PlayscriptRuntimeEmitter` | `PlayscriptRuntimeSession` class (extends `PlayscriptSessionScope`), `Registry`, `CreateChild()`, enums, lazy loader |
| `Script.g.cs` | `ScriptRegistry` | `Script` class with `Run()`, `RunAsync()` + pointer-based navigation returning `RenderResult?` subtypes (session-aware) |
| `Text.g.cs` | `ScriptRegistry` | `Text` class with `Render()`, `RenderAsync()` (session-aware) |

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
| SCPT012 | Async interface with sync implementation |
| SCPT013 | Sync interface with async implementation |

All codes defined in `EasyPlayscript.Generator/PlayscriptDiagnostics.cs`.

## Generator Testing Pattern

Tests in `EasyPlayscript.Tests/` use `CSharpGeneratorDriver` with:
- `TestAdditionalFile` (from `Utils/`) to simulate `.scpt` files
- `TestAnalyzerConfigOptionsProvider` for build properties (`PlayscriptOutputPath`, `PlayscriptAesKey`)

Pattern: create generator → add additional files → run driver → assert on generated syntax tree or diagnostics.

Emitter tests (`PlayscriptRegistryEmitterTests`, `PlayscriptRuntimeEmitterTests`) call emitters directly with hand-built `PlayscriptCompilationData` — no Roslyn driver needed.

`ScriptRegistryTests` uses `CSharpGeneratorDriver` with the `ScriptRegistry` generator (post-initialization, no .scpt files needed).

## LSP Server

`EasyPlayscript.LSP` is an executable targeting net10.0 using `OmniSharp.Extensions.LanguageServer`. It references Core (not Generator). Key components:

- `PlayscriptDocumentParser` — parses `.scpt` files into `ParsedDocument`; `ParseIncremental()` reuses cached block tokens when content is unchanged
- `PlayscriptDocumentSyncHandler` — open/change/close sync with **incremental** changes (`TextDocumentSyncKind.Incremental`), debounced at 300ms
- `PlayscriptSemanticTokensHandler` — semantic token highlighting
- `PositionMapper` — ANTLR ↔ LSP position conversion (ANTLR 1-based lines → LSP 0-based)
- `DocumentStore` — tracks open documents, stores current text, applies incremental edits via `TextEditApplier`
- `TextEditApplier` — applies `TextDocumentContentChangeEvent` range-based edits to a string

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
- `EasyPlayscript.Core/Runtime/RenderResult.cs` — abstract `RenderResult` base + sealed `SegmentRenderResult`, `LineRenderResult`, `ParagraphRenderResult`, `PageRenderResult` subtypes
- `EasyPlayscript.Core/DataModel/Segment.cs` — `Segment` class with `Items` (a segment is one part of a line, delimited by `+`)
- `EasyPlayscript.Core/DataModel/Line.cs` — `Line` class with `Segments` (a line contains one or more segments)
- `EasyPlayscript.Core/ImplementationAttribute.cs` — `[Implementation]` attribute (no scope — all services use parent-child chain)
- `EasyPlayscript.Core/Parsing/InterfaceDeclaration.cs` — `InterfaceDeclaration` with `IsAsync` property; `InterfaceType` enum
- `EasyPlayscript.Core/Parsing/ImplementationInfo.cs` — `ImplementationInfo` with `IsAsync` property
- `EasyPlayscript.Generator/ImplementationScanner.cs` — extracts `[Implementation]` methods, detects async via `INamedTypeSymbol`
- `EasyPlayscript.Sample/scripts/*.scpt` — example `.scpt` files

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
- `ScriptNavigator` also has async variants (`RenderNextLineAsync`, etc.) taking `Func<Line, Task<string>>`. The generated Script passes `RenderLineAsync` (which uses `await Runtime.DispatchCallAsync(call)`) as that callback
- `ScriptNavigator` returns `RenderResult?` subtypes from all `RenderNext*` methods (`LineRenderResult?`, `ParagraphRenderResult?`, `PageRenderResult?`). The callback still returns `string` — the navigator wraps it in the appropriate subtype with the pointer and boundary flags captured before advancing
- The generated `PlayscriptRuntimeSession` inherits from `PlayscriptSessionScope` (Core). The base holds the service dictionary and parent chain; the generated class adds `Registry`, `DispatchCall`, `CreateChild` override, and script/text loading
- `CreateChild()` returns `PlayscriptRuntimeSession` (covariant return). The child shares the same `Registry` instance as the parent
- `EasyPlayscript.LSP` targets net10.0 (not netstandard2.0 like Core/Generator) — it's an executable, not a library
- LSP uses incremental sync (`TextDocumentSyncKind.Incremental`). The client sends range-based edits, not full document text. `DocumentStore.ApplyChanges()` applies edits to the stored text, then calls `ParseIncremental()` which reuses cached block tokens when a block's `RawContent` is unchanged
