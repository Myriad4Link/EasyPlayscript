<p align="center">
  <img src="assets/super-ugly-logo-gotta-refactor-it-someday.png" alt="EasyPlayscript Logo" width="200"/>
</p>

<h1 align="center">EasyPlayscript</h1>

<p align="center">
  A C# type-safe playscript writing DSL designed for bridging the gap between texts and codes in NVL game development. Still in early development.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet" alt=".NET 10.0"/>
  <img src="https://img.shields.io/badge/.NET_Standard-2.0-512BD4?logo=dotnet" alt=".NET Standard 2.0"/>
  <img src="https://img.shields.io/badge/C%23-13.0-239120?logo=csharp" alt="C# 13"/>
  <img src="https://img.shields.io/badge/ANTLR-4.13-EC1C24" alt="ANTLR 4.13"/>
  <img src="https://img.shields.io/badge/License-MIT-F5A623" alt="License: MIT"/>
  <img src="https://img.shields.io/badge/status-early_development-F44336" alt="Early Development"/>
</p>

---

## Overview

EasyPlayscript lets you write game dialogue, UI text, and event scripts in a human-readable `.scpt` format (sorry AppleScript, wasn't knowing ya at the time I selected the extension) while keeping everything type-safe at compile time. A Roslyn source generator reads your scripts and generates the C# wiring — service dispatch, registry, navigation — so you never hand-write boilerplate.

EasyPlayscript focuses on playscript and text writing,
while the C# side provides type-safe contracts.
You describe and call functions in your `.scpt` files just like you would call interfaces in code.
The actual business logic is delegated to and lives in C#, keeping scripts clean and focused on narrative flow.

At build time, `.scpt` files are compiled to MessagePack for efficient runtime loading, with optional AES encryption to protect script content.

The two-pass ANTLR parser extracts structure first (blocks & interface definitions), 
then content (consumer calls, text). This enables incremental LSP editing and fast rebuilds.

## Before Quick Start ...

### Interface

An **interface** is a function signature declared at the top of a `.scpt` file. It defines *what* the script can call, but not *how* — the actual logic lives in C#.

```
interface enter(description: string) : void
interface exit(description: string) : void
interface effect(name: string, intensity: decimal) : void
interface set_music(track: string) : void
```

Think of it as a contract between the script writer and the programmer. The script writer says "I need an `effect` function that takes a name and intensity," and the C# side provides it. Parameters and return types are type-checked at compile time.

The `async` variant requires the C# implementation to return `Task<T>` (or `Task` for `void`). Async interfaces are properly awaited when using `RunAsync()` / `RenderNextLineAsync()`.

### Consumer call

A **consumer call** is how a script invokes an interface. It's written with an `@` prefix inside a script or text block:

```
script act_i_scene_i[
@effect("thunder", 1.0)
@enter("Thunder and Lightning. Enter three Witches.")

When shall we three meet again?
In thunder, lightning, or in rain?

When the hurly-burly's done,
When the battle's lost and won.

That will be ere the set of sun.

Fair is foul, and foul is fair;
Hover through the fog and filthy air.

@exit("They exit.")
]
```

Notice how the dialogue is just plain text — no quotes, no `@`. This is what the player sees. The `@` calls (`@effect`, `@enter`, `@exit`) are side effects handled by the game engine — playing thunder sounds, animating stage entrances, triggering transitions. The script stays focused on narrative; the C# side handles everything else.

At compile time, every consumer call is validated against declared interfaces:
- Wrong name → error (SCPT005: undeclared consumer call)
- Wrong argument count → error (SCPT008)
- Wrong argument types → error (SCPT007)

This means script writers get the same safety net as writing C# code — typos and mismatches are caught before the game ever runs.

### Script

A **script** is a block of paged narrative content — dialogue, cutscenes, event sequences. It supports paragraphs (separated by blank lines) and pages (separated by `/`).

```
script act_i_scene_i[
@effect("thunder", 1.0)
@enter("Thunder and Lightning. Enter three Witches.")

When shall we three meet again?
In thunder, lightning, or in rain?

When the hurly-burly's done,
When the battle's lost and won.
/

That will be ere the set of sun.

Where the place?
Upon the heath.
There to meet with Macbeth.
/

Fair is foul, and foul is fair;
Hover through the fog and filthy air.

@exit("They exit.")
]
```

The first `/` ends page 1 (the witches' opening exchange); page 2 continues the prophecy; page 3 climaxes with "Fair is foul."

Scripts are compiled to MessagePack at build time. At runtime, you can navigate them step by step:

```csharp
var script = session.GetScript(PlayscriptRuntimeSession.ScriptKey.act_i_scene_i);
script.RenderNextLine()        // "When shall we three meet again?"
script.RenderNextLine()        // "In thunder, lightning, or in rain?"
script.RenderNextParagraph()   // "When the hurly-burly's done,\nWhen the battle's lost and won."
script.RenderNextPage()        // "That will be ere the set of sun."  (skips to page 2)
```

Or just run everything at once with `script.Run()`.

### Text

A **text** block is for static content — play titles, credits, UI labels — anything that isn't paged dialogue.

```
text playbill[
The Tragedie of Macbeth
by William Shakespeare
]

text act_header[
Act I
]
```

Texts support consumer calls but have no pages or paragraphs. They're rendered in one call:

```csharp
string title = session.GetText(PlayscriptRuntimeSession.TextKey.playbill).Render();
```

### Implementation

An **implementation** is a C# method decorated with `[Implementation]` that provides the body for an interface declared in a `.scpt` file.

```csharp
public class StageSystem
{
    [Implementation]
    public void enter(string description)
    {
        Console.WriteLine($"  [stage] {description}");
        // animate characters onto stage
    }

    [Implementation]
    public void exit(string description)
    {
        Console.WriteLine($"  [stage] {description}");
        // animate characters off stage
    }

    [Implementation]
    public void effect(string name, double intensity)
    {
        Console.WriteLine($"  [effect] {name} (intensity: {intensity})");
        // play thunder, lightning, fog, etc.
    }

    [Implementation]
    public void set_music(string track)
    {
        Console.WriteLine($"  [music] now playing: {track}");
        // crossfade to new track
    }
}
```

The source generator wires every consumer call `@effect("thunder", 1.0)` in your scripts to the matching `[Implementation]` method. If an interface has no implementation, you get a compile-time error (SCPT009).

### Session

A **session** (`PlayscriptRuntimeSession`) is the runtime entry point. It holds your registered implementations and provides access to scripts and texts.

```csharp
var session = new PlayscriptRuntimeSession();
session.Register(new StageSystem());

session.GetScript(PlayscriptRuntimeSession.ScriptKey.act_i_scene_i).Run();
string title = session.GetText(PlayscriptRuntimeSession.TextKey.playbill).Render();
```

Sessions support **parent-child scoping**. A child session inherits all services from its parent and can override specific ones — useful for scene-specific behavior without duplicating registration:

```csharp
var global = new PlayscriptRuntimeSession();
global.Register(new StageSystem());

var actV = global.CreateChild();
actV.Register(new StageSystem()); // same type, different instance — e.g. with different music state
```

### Source generator

The **source generator** is a Roslyn `IIncrementalGenerator` that runs at compile time. It reads your `.scpt` files and emits C# code:

| Generated file | Contents |
|----------------|----------|
| `PlayscriptRegistry.g.cs` | `DispatchCall()` — switch that routes consumer calls to implementations |
| `PlayscriptRuntime.g.cs` | `PlayscriptRuntimeSession` class, enums for script/text keys, lazy loader |
| `Script.g.cs` | `Script` class with `Run()`, `RenderNextLine()`, navigation |
| `Text.g.cs` | `Text` class with `Render()` |

You never see or touch these files — they're generated fresh every build.

### Two-pass parser

The ANTLR parser processes `.scpt` files in two passes:

1. **Pass 1 (structure)** — extracts block types, names, page/paragraph boundaries, interface declarations
2. **Pass 2 (content)** — parses the actual script/text content inside `[...]` blocks

This split exists for performance. The LSP server can re-parse only the blocks whose content changed, skipping the structure pass entirely for unaffected blocks.

## Quick Start

### 1. Write a `.scpt` file

```
interface enter(description: string) : void
interface exit(description: string) : void
interface effect(name: string, intensity: decimal) : void

script act_i_scene_i[
@effect("thunder", 1.0)
@enter("Thunder and Lightning. Enter three Witches.")

When shall we three meet again?
In thunder, lightning, or in rain?

When the hurly-burly's done,
When the battle's lost and won.

That will be ere the set of sun.

Where the place?
Upon the heath.
There to meet with Macbeth.

Fair is foul, and foul is fair;
Hover through the fog and filthy air.

@exit("They exit.")
]
```

### 2. Implement the services in C\#

```csharp
using EasyPlayscript.Runtime;

public class StageSystem
{
    [Implementation]
    public void enter(string description)
    {
        Console.WriteLine($"  [stage] {description}");
    }

    [Implementation]
    public void exit(string description)
    {
        Console.WriteLine($"  [stage] {description}");
    }

    [Implementation]
    public void effect(string name, double intensity)
    {
        Console.WriteLine($"  [effect] {name} (intensity: {intensity})");
    }
}
```

### 3. Run it

```csharp
using EasyPlayscript.Generated;

var session = new PlayscriptRuntimeSession();
session.Register(new StageSystem());

var script = session.GetScript(PlayscriptRuntimeSession.ScriptKey.act_i_scene_i);
while (script.RenderNextLine() is { } line)
    Console.WriteLine(line);
```

Output:

```
  [effect] thunder (intensity: 1)
  [stage] Thunder and Lightning. Enter three Witches.
When shall we three meet again?
In thunder, lightning, or in rain?
When the hurly-burly's done,
When the battle's lost and won.
That will be ere the set of sun.
...
Fair is foul, and foul is fair;
Hover through the fog and filthy air.
  [stage] They exit.
```

## `.scpt` Syntax

### Interfaces

```
interface enter(description: string) : void
interface effect(name: string, intensity: decimal) : void
interface set_music(track: string) : void
async interface fetch_character(name: string) : string
```

- Declares consumer calls the script can invoke via `@name(args)`
- `async interface` requires `Task<T>` implementations; the generated `RunAsync()` / `RenderNextLineAsync()` properly await them

### Scripts

```
script act_i_scene_i[
@effect("thunder", 1.0)
@enter("Thunder and Lightning. Enter three Witches.")

When shall we three meet again?

When the hurly-burly's done,
When the battle's lost and won.
/

That will be ere the set of sun.
@exit("They exit.")
]
```

- `@name(args)` — consumer call dispatched to a registered `[Implementation]` method
- Plain text — dialogue and stage directions visible to the player
- Blank line — paragraph separator
- `/` — page separator

### Text blocks

```
text playbill[
The Tragedie of Macbeth
by William Shakespeare
]
```

- Static text blocks rendered via `session.GetText(key).Render()`

## Project Structure

| Project | Target | Description |
|---------|--------|-------------|
| `EasyPlayscript.Core` | netstandard2.0 | ANTLR parsers, data models, validation, `ScriptNavigator` |
| `EasyPlayscript.Generator` | netstandard2.0 | Roslyn `IIncrementalGenerator` — emits all `.g.cs` files |
| `EasyPlayscript.BuildTask` | netstandard2.0 | MSBuild task for binary `.scpt` compilation |
| `EasyPlayscript.LSP` | net10.0 | OmniSharp-based language server |
| `EasyPlayscript.Tests` | net9.0 | xUnit tests for Core + Generator |
| `EasyPlayscript.LSP.Tests` | net10.0 | xUnit tests for LSP |
| `EasyPlayscript.Sample` | net9.0 | Demo app with sample `.scpt` scripts |

## LSP Server

The LSP server provides editor integration for `.scpt` files:

- **Incremental sync** — only re-parses changed blocks
- **Semantic tokens** — syntax highlighting for interfaces, scripts, consumer calls
- **Diagnostics** — undeclared calls, type mismatches, duplicates

Run it directly:

```bash
dotnet run --project EasyPlayscript.LSP
```

Or configure it in your editor as an LSP server executable.

## Building

**Requires .NET 10.0.301** (see `global.json`).

```bash
# Build everything
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test EasyPlayscript.Tests

# Run the sample
dotnet run --project EasyPlayscript.Sample

# Rebuild & repack NuGet packages for local development
./pack-local.ps1
```

### Parent-child sessions

```csharp
var global = new PlayscriptRuntimeSession();
global.Register(new StageSystem());

var actV = global.CreateChild();
actV.Register(new StageSystem()); // override for Act V's different mood/music
actV.GetScript(key).Run();        // uses Act V's StageSystem
global.GetScript(key).Run();      // still uses the original
```

### Script navigation

```csharp
var script = session.GetScript(key);

script.RenderNextLine()          // returns string? — dispatches calls + returns text
script.RenderNextParagraph()     // lines joined by newline
script.RenderNextPage()          // paragraphs joined by blank line
script.JumpTo(new ScriptPointer(page, paragraph, line))
script.Reset()                   // rewinds to (0,0,0)
script.IsLastLineOfPage          // bool
```

### Async interfaces

```
async interface fetch_character(name: string) : string
```

```csharp
// Implementation must return Task<T>
[Implementation]
public async Task<string> fetch_character(string name)
{
    return await _db.GetCharacterBioAsync(name);
}

// Use async render to properly await
while (await script.RenderNextLineAsync() is { } line)
    Console.WriteLine(line);
```

## License

[MIT](LICENSE)
