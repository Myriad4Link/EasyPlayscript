# EasyPlayscript LSP Server — Implementation Plan

## Overview

Add an LSP (Language Server Protocol) server to the EasyPlayscript subproject, providing real-time IDE support for `.scpt` files: diagnostics, completions, hover, go-to-definition, document symbols, and folding ranges.

## Scope: MVP + Navigation

- Diagnostics (publishDiagnostics)
- Document Symbols
- Folding Ranges
- Completions (`@` + keywords + types)
- Hover (interface signatures)
- Go-to-Definition (consumer call → interface declaration)

## Library

**OmniSharp.Extensions.LanguageServer** 0.19.9 — standard C# LSP library (stdio transport).

## New Project: `EasyPlayscript.LSP`

**Target:** net8.0 console application  
**Dependencies:**
- `OmniSharp.Extensions.LanguageServer` 0.19.9
- `EasyPlayscript.Core` (project reference)

### Project Structure

```
EasyPlayscript/EasyPlayscript.LSP/
├── EasyPlayscript.LSP.csproj
├── Program.cs
├── Services/
│   ├── PlayscriptDocument.cs        # Per-document parsed state
│   ├── DocumentManager.cs           # Tracks open documents + parsed state
│   ├── PlayscriptWorkspace.cs       # Cross-file state (PlayscriptCompilationData)
│   └── AnalysisService.cs           # Orchestrates two-pass parse + validation
├── Mapping/
│   └── PositionHelper.cs            # ANTLR 1-based → LSP 0-based, content offset mapping
└── Handlers/
    ├── PlayscriptSyncHandler.cs     # didOpen/didChange/didClose → triggers diagnostics
    ├── PlayscriptCompletionHandler.cs
    ├── PlayscriptHoverHandler.cs
    ├── PlayscriptDefinitionHandler.cs
    ├── PlayscriptDocumentSymbolHandler.cs
    └── PlayscriptFoldingRangeHandler.cs
```

---

## File-by-File Design

### 1. `EasyPlayscript.LSP.csproj`

Net8.0 console. References:
- `OmniSharp.Extensions.LanguageServer` 0.19.9
- `EasyPlayscript.Core` (project reference)

### 2. `Program.cs`

Bootstrap `LanguageServer.From()` with stdio transport. Register all handlers and services via `.WithHandler<T>()` and `.AddSingleton<T>()`. Document selector filter: `**/*.scpt`.

### 3. `Services/PlayscriptDocument.cs`

Holds per-document state:

| Field | Type | Purpose |
|-------|------|---------|
| `Content` | `string` | Full text |
| `StructureResult` | `StructureParseResult` | Pass 1 output |
| `ContentParsers` | `List<PlayscriptContentParser>` | Pass 2 parsers per block |
| `BlockOffsets` | `List<BlockOffset>` | Bracket offset map for position correction |
| `AllErrors` | `List<PlayscriptError>` | Merged Pass 1 + Pass 2 errors |
| `ValidationDiag` | `List<ValidationDiagnostic>` | From InterfaceValidator |

`BlockOffset` stores the `(contentStartLine, contentStartCol)` of each `[...]` block's raw content, used to convert content-relative positions back to absolute file positions.

### 4. `Services/DocumentManager.cs`

`ConcurrentDictionary<DocumentUri, PlayscriptDocument>`. Methods:
- `Open(uri, text)` → parse + store
- `Update(uri, text)` → reparse + store
- `Close(uri)` → remove
- `Get(uri)` → retrieve parsed state

Fires `DocumentChanged` event for workspace revalidation.

### 5. `Services/PlayscriptWorkspace.cs`

Wraps `PlayscriptCompilationData`. On any document change:
1. Rebuild cross-file state from all open documents
2. Run `InterfaceValidator.ValidateUndeclaredCalls()`, `ValidateDuplicateSignatures()`, `ValidateArgumentTypes()`
3. Update validation diagnostics per document

Provides:
- `Interfaces` — all interface declarations across workspace
- `GetValidationDiagnostics(uri)` — validation errors for a document

### 6. `Services/AnalysisService.cs`

Orchestrates the two-pass pipeline (same logic as `PlayscriptGenerator.ParseSingleFile` + `MergeBlocks`).

Called on every document open/change:
1. `PlayscriptStructureHelper.ParseStructureWithErrors(content)` — Pass 1
2. For each block: `PlayscriptContentHelper.Parse(trimmedContent)` → `parser.scriptContent()` — Pass 2
3. `PlayscriptCodeBuilder.BuildScriptFromContent()` or `BuildTextFromContent()` — AST
4. Store results in `PlayscriptDocument`
5. Trigger `PlayscriptWorkspace.Revalidate()`
6. Publish diagnostics via `ILanguageServerFacade.TextDocument.SendPublishDiagnostics`

### 7. `Mapping/PositionHelper.cs`

Static helpers:

| Method | Purpose |
|--------|---------|
| `ToLspPosition(int antlrLine, int antlrCol)` | 1-based → 0-based |
| `ToLspPosition(int contentLine, int contentCol, BlockOffset block)` | Content-relative → absolute file position |
| `ToLspRange(int startLine, int startCol, int endLine, int endCol)` | Single-token range |
| `ToLspDiagnostic(PlayscriptError error)` | ANTLR error → LSP Diagnostic |
| `ToLspDiagnostic(ValidationDiagnostic diag)` | Validation error → LSP Diagnostic |

**Position mapping detail:** Content grammar positions are relative to the trimmed raw content inside `[...]`. To map back to the original `.scpt` file:
- From Pass 1, the `RAW_CONTENT` token's start line/col is known
- Content is trimmed with `Trim('\r', '\n')`, which may shift the first line
- Formula: `originalLine = block.contentStartLine + contentLine - 1`, `originalCol = contentCol`

---

## Handler Designs

### 8. `Handlers/PlayscriptSyncHandler.cs`

Extends `TextDocumentSyncHandlerBase`. `TextDocumentSyncKind.Full`.

| Event | Action |
|-------|--------|
| `didOpen` | Store in DocumentManager → Analyze → Publish diagnostics |
| `didChange` | Update in DocumentManager → Re-analyze → Republish diagnostics |
| `didClose` | Remove from DocumentManager → Clear diagnostics |

DocumentSelector: `{ Pattern = "**/*.scpt" }`, Language: `"playscript"`.

### 9. `Handlers/PlayscriptCompletionHandler.cs`

`ICompletionHandler`. TriggerCharacters: `@`, `"`.

| Context | Completions |
|---------|-------------|
| After `@` | Interface names from `PlayscriptWorkspace.Interfaces` (Kind: Interface) |
| Top-level | `script`, `text`, `interface` keywords (Kind: Keyword) |
| Inside interface param list | `string`, `int`, `decimal`, `bool`, `void` type keywords |

### 10. `Handlers/PlayscriptHoverHandler.cs`

`IHoverHandler`.

| Cursor on | Hover content |
|-----------|---------------|
| `@identifier` | Interface signature: `name(paramName: type, ...): returnType` |
| Keyword | Brief description of the keyword |

### 11. `Handlers/PlayscriptDefinitionHandler.cs`

`IDefinitionHandler`.

| Cursor on | Jump to |
|-----------|---------|
| `@identifier` in consumer call | `InterfaceDeclaration` (has `FilePath`, `Line`, `Col`) |

Cross-file jumps supported — InterfaceDeclaration tracks FilePath.

### 12. `Handlers/PlayscriptDocumentSymbolHandler.cs`

`IDocumentSymbolHandler`.

| Symbol | Kind |
|--------|------|
| Script block | `SymbolKind.Class` |
| Text block | `SymbolKind.Struct` |
| Interface declaration | `SymbolKind.Interface` |

Range = full block, SelectionRange = name token.

### 13. `Handlers/PlayscriptFoldingRangeHandler.cs`

`IFoldingRangeHandler`.

| Foldable | Range |
|----------|-------|
| `[...]` block | LBRACKET line → RBRACKET line |
| Multi-line interface declaration | First line → last line |

---

## Dependency Graph

```
Program.cs
  └── registers all handlers + services

PlayscriptSyncHandler
  ├── DocumentManager (store/retrieve)
  ├── AnalysisService (parse + validate)
  └── ILanguageServerFacade (publish diagnostics)

AnalysisService
  ├── PlayscriptStructureHelper (Pass 1)
  ├── PlayscriptContentHelper (Pass 2)
  ├── PlayscriptCodeBuilder (AST)
  ├── PlayscriptWorkspace (cross-file state)
  └── PositionHelper (coordinate mapping)

All other handlers
  ├── DocumentManager (get parsed state)
  └── PlayscriptWorkspace (get cross-file interfaces)
```

---

## Estimated Line Counts

| File | Lines |
|------|-------|
| `EasyPlayscript.LSP.csproj` | 15 |
| `Program.cs` | 40 |
| `Services/PlayscriptDocument.cs` | 50 |
| `Services/DocumentManager.cs` | 80 |
| `Services/PlayscriptWorkspace.cs` | 120 |
| `Services/AnalysisService.cs` | 200 |
| `Mapping/PositionHelper.cs` | 80 |
| `Handlers/PlayscriptSyncHandler.cs` | 80 |
| `Handlers/PlayscriptCompletionHandler.cs` | 120 |
| `Handlers/PlayscriptHoverHandler.cs` | 80 |
| `Handlers/PlayscriptDefinitionHandler.cs` | 70 |
| `Handlers/PlayscriptDocumentSymbolHandler.cs` | 80 |
| `Handlers/PlayscriptFoldingRangeHandler.cs` | 50 |
| **Total** | **~1,065** |

---

## Code Reuse from EasyPlayscript.Core

| Component | Source | Reused as-is? |
|-----------|--------|---------------|
| `PlayscriptStructureHelper` | EasyPlayscript.Core | Yes |
| `PlayscriptContentHelper` | EasyPlayscript.Core | Yes |
| `PlayscriptCodeBuilder` | EasyPlayscript.Core | Yes |
| `InterfaceValidator` | EasyPlayscript.Core | Yes |
| `PlayscriptCompilationData` | EasyPlayscript.Core | Yes |
| `InterfaceDeclaration` | EasyPlayscript.Core | Yes |
| `PlayscriptError` | EasyPlayscript.Core | Yes |
| `ValidationDiagnostic` | EasyPlayscript.Core | Yes |
| `PlayscriptDiagnostics` codes | EasyPlayscript.EasyPlayscript | Copy SCPT code constants |
| `MakeLocation` logic | PlayscriptGenerator.cs:180 | Extract to PositionHelper |

---

## Key Technical Challenges

### 1. Position Mapping (Content → File)

Content grammar positions are relative to the trimmed raw content inside `[...]`. The `RAW_CONTENT` token's start position from Pass 1 provides the base offset, but `Trim('\r', '\n')` shifts lines. A `PositionMapper` service is needed.

### 2. Cross-File Workspace State

`InterfaceValidator` needs `PlayscriptCompilationData` aggregated across all files. The LSP must maintain workspace-wide state, revalidating all files when any interface declaration changes.

### 3. ANTLR Re-parsing Cost

Full reparse on every keystroke. ANTLR is fast for small-to-medium `.scpt` files, but debouncing (300-500ms) is advisable. The `CancellationToken` support already in `PlayscriptCodeBuilder` helps.

### 4. netstandard2.0 Constraint

`EasyPlayscript.Core` targets netstandard2.0 — no conflict with net8.0 LSP server. OmniSharp also supports netstandard2.0.

---

## Open Questions

1. **Solution file:** Add `EasyPlayscript.LSP` to `Kuraitsuku.sln`?
2. **Diagnostic source string:** Use `"EasyPlayscript"` or `"playscript"` as the LSP diagnostic source?
3. **Completion snippets:** Should `script`/`text` completions insert full block templates with `[...]` brackets, or just the keyword?
4. **Cross-file validation:** Should the workspace track all `.scpt` files in the project directory (glob), or only opened documents?
