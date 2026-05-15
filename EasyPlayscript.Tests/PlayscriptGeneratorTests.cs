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
        @script("load tooltip")[
        你好。
        这里是……？

        啊、您好！

        请问你是？

        @transistion("fade_out")
        ]
        """;

    private const string TextBlockExample = """
        @text("intro text")[
        你好，欢迎来到这个世界。
        ]
        """;

    private static string GenerateCode(params (string name, string content)[] files)
    {
        var generator = new PlayscriptGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

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
    public void GeneratedCode_UsesScriptRegistry()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("using EasyPlayscript.Generated;", code);
    }

    [Fact]
    public void ScriptBlock_ContentIsPopulated()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("new ScriptBlock { Content = { \"你好。这里是……？\"", code);
    }
}
