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

    private const string StandaloneExternalCallExample = """
        @init("setup")
        """;

    private static string GenerateCode(string fileName, string content)
    {
        var generator = new PlayscriptGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.AddAdditionalTexts(
            ImmutableArray.Create<AdditionalText>(
                new TestAdditionalFile($"./{fileName}.scpt", content)));

        var compilation = CSharpCompilation.Create(nameof(PlayscriptGeneratorTests));
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var newCompilation, out _);

        var generatedFile = newCompilation.SyntaxTrees
            .Single(t => Path.GetFileName(t.FilePath) == $"{fileName}.g.cs");

        return generatedFile.GetText().ToString();
    }

    [Fact]
    public void ScriptBlock_GeneratesRegistryScriptCall()
    {
        var code = GenerateCode("Example", ScriptBlockExample);
        Assert.Contains("// TODO: Script(\"load tooltip\")", code);
    }

    [Fact]
    public void TextBlock_GeneratesRegistryTextCall()
    {
        var code = GenerateCode("TextExample", TextBlockExample);
        Assert.Contains("// TODO: Text(\"intro text\")", code);
    }

    [Fact]
    public void StandaloneExternalCall_GeneratesTodoStub()
    {
        var code = GenerateCode("Standalone", StandaloneExternalCallExample);
        Assert.Contains("// TODO: top-level external call @init(\"setup\")", code);
    }

    [Fact]
    public void GeneratedCode_ContainsRunMethod()
    {
        var code = GenerateCode("Example", ScriptBlockExample);
        Assert.Contains("public static void Run(ScriptRegistry registry)", code);
    }

    [Fact]
    public void GeneratedCode_UsesScriptRegistry()
    {
        var code = GenerateCode("Example", ScriptBlockExample);
        Assert.Contains("using EasyPlayscript.Generated;", code);
    }

    [Fact]
    public void GeneratedCode_UsesEasyPlayscript()
    {
        var code = GenerateCode("Example", ScriptBlockExample);
        Assert.Contains("using EasyPlayscript;", code);
    }
}
