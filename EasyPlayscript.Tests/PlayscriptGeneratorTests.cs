using System.Collections.Immutable;
using System.IO;
using System.Linq;
using EasyPlayscript.Tests.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptGeneratorTests
{
    private const string ScriptBlockExample = """
        .script("load tooltip")[
        你好。
        这里是……？

        啊、您好！

        请问你是？

        @transition("fade_out")
        ]
        """;

    private const string TextBlockExample = """
        .text("intro text")[
        你好，欢迎来到这个世界。
        ]
        """;

    private const string TestOutputPath = "test-scripts.bin";
    private const string TestAesKey = "test-key-1234567";

    private static string GenerateCode(params (string name, string content)[] files)
    {
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(
            ("build_property.PlayscriptOutputPath", TestOutputPath),
            ("build_property.PlayscriptAesKey", TestAesKey));

        var generator = new PlayscriptGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            optionsProvider: optionsProvider);

        var additionalFiles = files
            .Select(f => new TestAdditionalFile($"./{f.name}.scpt", f.content))
            .ToImmutableArray<AdditionalText>();

        driver = driver.AddAdditionalTexts(additionalFiles);

        var compilation = CSharpCompilation.Create(nameof(PlayscriptGeneratorTests));
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var newCompilation, out _);

        var generatedFile = newCompilation.SyntaxTrees
            .Single(t => Path.GetFileName(t.FilePath) == "Registry.g.cs");

        return generatedFile.GetText().ToString();
    }

    [Fact]
    public void ScriptBlock_GeneratesStaticProperty()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("public static Script LOAD_TOOLTIP", code);
    }

    [Fact]
    public void TextBlock_GeneratesStaticProperty()
    {
        var code = GenerateCode(("TextExample", TextBlockExample));
        Assert.Contains("public static Text INTRO_TEXT", code);
    }

    [Fact]
    public void GeneratedCode_ContainsRegistryClass()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("public static class Registry", code);
    }

    [Fact]
    public void GeneratedCode_UsesEasyPlayscript()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("using EasyPlayscript;", code);
    }

    [Fact]
    public void GeneratedCode_UsesGeneratedNamespace()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("using EasyPlayscript.Generated;", code);
    }

    [Fact]
    public void ScriptBlock_ContentIsPopulated()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("PlayscriptLoader", code);
    }

    [Fact]
    public void GeneratedCode_ReferencesPlayscriptLoader()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("PlayscriptLoader", code);
    }

    [Fact]
    public void GeneratedCode_HasLazyLoaders()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("Lazy<", code);
        Assert.Contains("LoadScripts", code);
        Assert.Contains("LoadTexts", code);
    }

    [Fact]
    public void GeneratedCode_HasLazyDeclarations()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("_scripts", code);
        Assert.Contains("_texts", code);
    }

    [Fact]
    public void GeneratedCode_EmbedsOutputPath()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains(TestOutputPath, code);
    }

    [Fact]
    public void GeneratedCode_EmbedsAesKey()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains(TestAesKey, code);
    }

    private static ImmutableArray<Diagnostic> GenerateDiagnostics(params (string name, string content)[] files)
    {
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(
            ("build_property.PlayscriptOutputPath", TestOutputPath),
            ("build_property.PlayscriptAesKey", TestAesKey));

        var generator = new PlayscriptGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            optionsProvider: optionsProvider);

        var additionalFiles = files
            .Select(f => new TestAdditionalFile($"./{f.name}.scpt", f.content))
            .ToImmutableArray<AdditionalText>();

        driver = driver.AddAdditionalTexts(additionalFiles);

        var compilation = CSharpCompilation.Create(nameof(PlayscriptGeneratorTests));
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        return diagnostics;
    }

    [Fact]
    public void DuplicateScriptName_SameFile_ReportsSCPT004()
    {
        const string content = """
                               .script("foo")[
                               Hello
                               ]
                               .script("foo")[
                               World
                               ]
                               """;
        var diagnostics = GenerateDiagnostics(("dup", content));
        Assert.Contains(diagnostics, d => d.Id == "SCPT004");
    }

    [Fact]
    public void DuplicateTextName_SameFile_ReportsSCPT004()
    {
        const string content = """
                               .text("intro")[
                               Hello
                               ]
                               .text("intro")[
                               World
                               ]
                               """;
        var diagnostics = GenerateDiagnostics(("dup", content));
        Assert.Contains(diagnostics, d => d.Id == "SCPT004");
    }

    [Fact]
    public void DuplicateScriptName_CrossFile_ReportsSCPT004()
    {
        var fileA = """
            .script("shared")[
            From file A
            ]
            """;
        var fileB = """
            .script("shared")[
            From file B
            ]
            """;
        var diagnostics = GenerateDiagnostics(("fileA", fileA), ("fileB", fileB));
        Assert.Contains(diagnostics, d => d.Id == "SCPT004");
    }

    [Fact]
    public void DuplicateTextName_CrossFile_ReportsSCPT004()
    {
        const string fileA = """
                             .text("shared")[
                             From file A
                             ]
                             """;
        const string fileB = """
                             .text("shared")[
                             From file B
                             ]
                             """;
        var diagnostics = GenerateDiagnostics(("fileA", fileA), ("fileB", fileB));
        Assert.Contains(diagnostics, d => d.Id == "SCPT004");
    }

    [Fact]
    public void NoDuplicate_NoSCPT004()
    {
        var fileA = """
            .script("alpha")[
            Alpha
            ]
            """;
        var fileB = """
            .script("beta")[
            Beta
            ]
            """;
        var diagnostics = GenerateDiagnostics(("fileA", fileA), ("fileB", fileB));
        Assert.DoesNotContain(diagnostics, d => d.Id == "SCPT004");
    }

    [Fact]
    public void InvalidContent_ReportsDiagnostic()
    {
        const string content = """
                               .script("test")[
                               @invalid(unclosed
                               ]
                               """;
        var diagnostics = GenerateDiagnostics(("bad", content));
        Assert.Contains(diagnostics, d => d.Id is "SCPT002" or "SCPT003");
    }
}
