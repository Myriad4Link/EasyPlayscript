# EasyPlayscript.Generator

Roslyn incremental source generator for [EasyPlayscript](https://github.com/Myriad4Link/EasyPlayscript), a type-safe scripting language for narrative-driven game development.

At compile time this generator reads `.scpt` files from the host project, parses them with the ANTLR-based two-pass parser, validates interfaces/implementations/cross-file references, and emits four generated files:

| File | Purpose |
|------|---------|
| `PlayscriptRegistry.g.cs` | Service dispatch (`DispatchCall`, `DispatchCallAsync`) |
| `PlayscriptRuntime.g.cs` | `PlayscriptRuntimeSession` + registry + enums |
| `Script.g.cs` | Script class with `Run()`, `RunAsync()` and pointer-based navigation |
| `Text.g.cs` | Text class with `Render()`, `RenderAsync()` |

## Usage

Add a reference to this package in your `.csproj` (alongside `EasyPlayscript.Core`):

```xml
<PackageReference Include="EasyPlayscript.Core" Version="1.0.0" />
<PackageReference Include="EasyPlayscript.Generator" Version="1.0.0"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

Place `.scpt` files anywhere in your project (e.g., a `scripts/` directory). They are automatically picked up as additional files.

The generator requires MSBuild property `PlayscriptOutputPath` (set in your `.csproj`):

```xml
<PropertyGroup>
  <PlayscriptOutputPath>$(MSBuildThisFileDirectory)Generated\Playscripts\</PlayscriptOutputPath>
</PropertyGroup>
```

## Diagnostics

| Code | Description |
|------|-------------|
| SCPT002 | Lexer error |
| SCPT003 | Parser error |
| SCPT004 | Duplicate script/text name |
| SCPT005 | Undeclared consumer call |
| SCPT006 | Duplicate interface signature |
| SCPT007 | Argument type mismatch |
| SCPT008 | Argument count mismatch |
| SCPT009 | Missing `[Implementation]` method |
| SCPT010 | Duplicate `[Implementation]` |
| SCPT011 | Unused `[Implementation]` (warning) |
| SCPT012 | Async interface with sync implementation |
| SCPT013 | Sync interface with async implementation |

## License

MIT — see the [root LICENSE](../LICENSE).
