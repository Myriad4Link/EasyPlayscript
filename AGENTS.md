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
./pack-local.ps1                          # Rebuild, repack NuGet packages into nuget-local/, + publish LSP to published/
./coverage.ps1                            # Run tests + generate HTML coverage report (requires `reportgenerator` on PATH)
```

**Note**: `dotnet test` takes the project path as a positional argument, not `--project`. Use `dotnet test EasyPlayscript.LSP.Tests`, not `dotnet test --project EasyPlayscript.LSP.Tests`.

**SDK**: .NET 10.0.301 required (`global.json` with `rollForward: latestMinor`).

**NuGet lock issue**: Running the LSP via `dotnet run --project` loads `Antlr4.Runtime.Standard.dll` from the build output, which can lock the same DLL in the NuGet cache for other projects. The fix: `pack-local.ps1` now publishes the LSP to `published/EasyPlayscript.LSP/` — point your editor/LSP config at `published/EasyPlayscript.LSP/EasyPlayscript.LSP.exe` instead of `dotnet run`. If restore still fails, use `dotnet build --no-restore`.

**MSBuild diagnostic verbosity**: To see how the build task is invoked (and which `.scpt` files it's processing), build with `dotnet build -v:n` or higher. Useful when `playscripts.bin` is missing or stale.

**Test counts**: 515 (Core/Generator) + 192 (LSP) = 707. Last known green baseline is recorded in commit messages; if a count drops, find the missing case by running `dotnet test --list-tests`.

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

Code constants in `EasyPlayscript.Core/Parsing/DiagnosticCodes.cs`; Roslyn `DiagnosticDescriptor` mapping in `EasyPlayscript.Generator/PlayscriptDiagnostics.cs`. Both must be kept in sync when adding codes.

## Generator Testing Pattern

Tests in `EasyPlayscript.Tests/` use `CSharpGeneratorDriver` with:
- `TestAdditionalFile` (from `Utils/`) to simulate `.scpt` files
- `TestAnalyzerConfigOptionsProvider` for build properties (`PlayscriptOutputPath` only — `PlayscriptAesKey` is build-task-only, not generator input)

Pattern: create generator → add additional files → run driver → assert on generated syntax tree or diagnostics.

Emitter tests (`PlayscriptRegistryEmitterTests`, `PlayscriptRuntimeEmitterTests`) call emitters directly with hand-built `PlayscriptCompilationData` — no Roslyn driver needed.

`ScriptRegistryTests` uses `CSharpGeneratorDriver` with the `ScriptRegistry` generator (post-initialization, no .scpt files needed).

`PlayscriptLoaderTests` covers the `PlayscriptLoader` API — encrypt/decrypt round trips, `LoadScripts`/`LoadTexts`/`LoadData` overloads, passphrase-optional paths. Required reading if you change `PlayscriptLoader.cs`.

## LSP Server

`EasyPlayscript.LSP` is an executable targeting net10.0 using `OmniSharp.Extensions.LanguageServer`. It references Core (not Generator). Key components:

- `PlayscriptDocumentParser` — parses `.scpt` files into `ParsedDocument`; `ParseIncremental()` reuses cached block tokens when content is unchanged; runs Pass 2 via `PlayscriptPipeline.ProcessFile`
- `PlayscriptDocumentSyncHandler` — open/change/close sync with **incremental** changes (`TextDocumentSyncKind.Incremental`), debounced at 300ms; `PublishDiagnostics` merges three sources: `doc.Errors`, `doc.ValidationDiagnostics`, and cross-file diagnostics from `WorkspaceIndex`
- `PlayscriptSemanticTokensHandler` — semantic token highlighting
- `PositionMapper` — ANTLR ↔ LSP position conversion (ANTLR 1-based lines → LSP 0-based); also converts `ValidationDiagnostic` → LSP `Diagnostic` with column clamping, CRLF handling, severity mapping (SCPT011→Warning else Error)
- `DocumentStore` — tracks open documents, stores current text, applies incremental edits via `TextEditApplier`, wires through to `WorkspaceIndex` on open/change/close
- `TextEditApplier` — applies `TextDocumentContentChangeEvent` range-based edits to a string
- `WorkspaceIndex` — insertion-ordered aggregation of per-file `PlayscriptCompilationData`; merges and re-runs `PlayscriptPipeline.Validate` on every change, routes cross-file diagnostics (SCPT004–008) by file path. Single-root only; multi-root deferred.

## Key Files

- `EasyPlayscript.Core/Parsing/PlayscriptPipeline.cs` — orchestrates validation
- `EasyPlayscript.Core/Parsing/InterfaceValidator.cs` — cross-file interface validation
- `EasyPlayscript.Core/Parsing/ImplementationValidator.cs` — validates `[Implementation]` method presence and duplicates
- `EasyPlayscript.Core/DataModel/PlayscriptLoader.cs` — encrypt/decrypt + LoadScripts/LoadTexts/LoadData overloads
- `EasyPlayscript.Generator/PlayscriptGenerator.cs` — main generator entry point, emits all `.g.cs` files
- `EasyPlayscript.Generator/PlayscriptRegistryEmitter.cs` — generates `PlayscriptRegistry.g.cs` with `DispatchCall()` using `session.Get<T>()`
- `EasyPlayscript.Generator/PlayscriptRuntimeEmitter.cs` — generates `PlayscriptRuntime.g.cs` (`PlayscriptRuntimeSession` class extending `PlayscriptSessionScope`)
- `EasyPlayscript.Generator/ScriptRegistry.cs` — generates `Script.g.cs` and `Text.g.cs` (post-initialization)
- `EasyPlayscript.BuildTask/PlayscriptBuildTask.cs` — MSBuild task entry point; reads `AesKey` and calls `PlayscriptLoader.Encrypt`
- `EasyPlayscript.LSP/Services/WorkspaceIndex.cs` — cross-file validation aggregation; routes SCPT004–008 diagnostics by file path, single-root only
- `EasyPlayscript.Core/PlayscriptSessionScope.cs` — base class with `ConcurrentDictionary` services, parent chain, `Register<T>`, `Get<T>`, `CreateChild`
- `EasyPlayscript.Core/ScriptNavigator.cs` — pointer-based navigation for Script (RenderNext*, IsLast*, JumpTo, Reset); returns `RenderResult?` subtypes
- `EasyPlayscript.Core/ScriptPointer.cs` — immutable value type for script position (pageIndex, paragraphIndex, lineIndex)
- `EasyPlayscript.Core/Runtime/RenderResult.cs` — abstract `RenderResult` base + sealed `SegmentRenderResult`, `LineRenderResult`, `ParagraphRenderResult`, `PageRenderResult` subtypes
- `EasyPlayscript.Core/DataModel/Segment.cs` / `Line.cs` — `Segment` (a part of a line, delimited by `+`) and `Line` (one or more `Segment`s)
- `EasyPlayscript.Core/ImplementationAttribute.cs` — `[Implementation]` attribute (no scope — all services use parent-child chain)
- `EasyPlayscript.Generator/ImplementationScanner.cs` — extracts `[Implementation]` methods, detects async via `INamedTypeSymbol`
- `EasyPlayscript.Sample/scripts/*.scpt` — example `.scpt` files
- `docs/RELEASE-READINESS.md` — what's shippable for 1.0, what's still open

## Gotchas

- The `.uid` files are JetBrains Rider cache — ignore them
- `EasyPlayscript.Sample` references NuGet packages (not project references). After changing Core, Generator, or BuildTask, run `./pack-local.ps1` before building the Sample
- `nuget-local/` is the local NuGet feed; `NuGet.Config` clears default sources and adds only `nuget.org` + `./nuget-local`
- No CI workflows exist — this is a local development repo
- `Script.g.cs` and `Text.g.cs` are emitted via `RegisterPostInitializationOutput` (runs before other generators). They reference `PlayscriptRuntimeSession` by name, which is generated later. This works because all generated sources compile together
- `PlayscriptRegistry.DispatchCall` switch cases use `{ }` blocks to scope local variables — C# switch cases share scope without blocks
- `ScriptNavigator` (Core) owns all pointer state; the generated `Script` class delegates to it. The navigator takes a `Func<Line, string>` render callback so it can be tested without a runtime. Async variants take `Func<Line, Task<string>>`. Flags are captured **before** the pointer advances (they describe the rendered unit, not the next position)
- The generated `PlayscriptRuntimeSession` inherits from `PlayscriptSessionScope` (Core). The base holds the service dictionary and parent chain; the generated class adds `Registry`, `DispatchCall`, `CreateChild` override (covariant return), and script/text loading
- `CreateChild()` returns `PlayscriptRuntimeSession` and shares the same `Registry` instance as the parent
- `EasyPlayscript.LSP` targets net10.0 (not netstandard2.0 like Core/Generator/BuildTask) — it's an executable, not a library
- LSP uses incremental sync (`TextDocumentSyncKind.Incremental`). The client sends range-based edits, not full document text. `DocumentStore.ApplyChanges()` applies edits to the stored text, then calls `ParseIncremental()` which reuses cached block tokens when a block's `RawContent` is unchanged
- SCPT009–SCPT013 (implementation-side diagnostics) are **not** surfaced by the LSP — the LSP has no access to Roslyn symbols or `[Implementation]` methods. The Roslyn source generator and MSBuild task still report them at compile time
- The LSP diagnostic pipeline merges three sources (`doc.Errors`, `doc.ValidationDiagnostics`, `workspace.GetAllDiagnostics`) and dedupes by `line:col:code` before publishing
- SCPT004 attribution is "first-merged file" (deterministic via insertion-ordered dictionary). SCPT006 attribution is "second-seen file"
