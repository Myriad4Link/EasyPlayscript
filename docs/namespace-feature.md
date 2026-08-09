# Namespace Feature — Implementation Plan

## Overview

Add `namespace` declaration to `.scpt` files, enabling script/text/interface namespacing.

### Key Design Decisions

1. **Syntax**: `namespace Alice.Bob` at top of file (before blocks/interfaces)
2. **Consumer calls**: `@ns1.ns2.log(param)` — dot-qualified identifier
3. **Session API**: `session.GetScript("Alice.Bob.loading")` — dot-qualified name string
4. **Resolution**: Same-namespace first, then global uniqueness check
5. **Default**: No `namespace` = root namespace (backward compatible)
6. **Path warning**: Full relative path → expected namespace (SCPT014)

---

## Phase 1: Grammar (.g4) Changes + ANTLR Regeneration

### Structure Grammar

**Lexer** (`PlayscriptStructureLexer.g4`):
- Add `NAMESPACE: 'namespace';` token

**Parser** (`PlayscriptStructureParser.g4`):
- Change `playscript` rule:
  ```
  playscript : namespaceDeclaration? topLevelStatement* EOF ;
  namespaceDeclaration : NAMESPACE IDENTIFIER (DOT IDENTIFIER)* ;
  ```

### Content Grammar

**Lexer** (`PlayscriptContentLexer.g4`):
- In `IN_CALL` mode: add `DOT: '.';` token

**Parser** (`PlayscriptContentParser.g4`):
- Change `consumerCall` rule:
  ```
  consumerCall : AT qualifiedName LPAREN (argument (COMMA argument)*)? RPAREN ;
  qualifiedName : IDENTIFIER (DOT IDENTIFIER)* ;
  ```

### Regeneration

```bash
# Structure
java -jar antlr-4.13.2-complete.jar -Dlanguage=CSharp -visitor -no-listener PlayscriptStructureLexer.g4 -o EasyPlayscript.Core/Parsing/Visitor/Structure
java -jar antlr-4.13.2-complete.jar -Dlanguage=CSharp -visitor -no-listener PlayscriptStructureParser.g4 -o EasyPlayscript.Core/Parsing/Visitor/Structure

# Content
java -jar antlr-4.13.2-complete.jar -Dlanguage=CSharp -visitor -no-listener PlayscriptContentLexer.g4 -o EasyPlayscript.Core/Parsing/Visitor/Content
java -jar antlr-4.13.2-complete.jar -Dlanguage=CSharp -visitor -no-listener PlayscriptContentParser.g4 -o EasyPlayscript.Core/Parsing/Visitor/Content
```

---

## Phase 2: Core Data Model

- `StructureParseResult`: Add `string? Namespace`
- `InterfaceDeclaration`: Add `string? Namespace`
- `ConsumerCallItem`: Add `string? Namespace`
- `PlayscriptCompilationData`: Store by qualified key, add `GetQualifiedName()`, track ambiguity

---

## Phase 3: Pipeline & Validation

- `PlayscriptPipeline.ProcessFile`: Accept namespace, register with qualified names
- `InterfaceValidator`: Same-namespace-first resolution, dot-qualified call support
- `PlayscriptPipeline.Validate`: Wire namespace through validation

---

## Phase 4: Generator Emitters

- `PlayscriptRuntimeEmitter`: Qualified keys + unqualified aliases, updated enums
- `PlayscriptRegistryEmitter`: Qualified identifiers in DispatchCall switch
- `PlayscriptGenerator`: Wire namespace through pipeline

---

## Phase 5: Script/Text Registry Generator

- `ScriptRegistry`: Update to handle qualified names if needed

---

## Phase 6: LSP Server

- `PlayscriptDocumentParser`: Parse namespace, pass to pipeline
- `WorkspaceIndex`: Namespace-aware merging and validation

---

## Phase 7: Path Mismatch Warning (SCPT014)

- New diagnostic for namespace/path mismatch
- Compute expected namespace from file path relative to project directory

---

## Phase 8: Integration

- Update sample `.scpt` files with namespace declarations
- Verify `dotnet run --project EasyPlayscript.Sample` works
