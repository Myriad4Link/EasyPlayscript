using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EasyPlayscript.Tests;

public class ScriptRegistryTests
{
    private static GeneratorDriverRunResult RunGenerator()
    {
        var generator = new ScriptRegistry();
        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(nameof(ScriptRegistryTests),
            [CSharpSyntaxTree.ParseText("")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        return driver.RunGenerators(compilation).GetRunResult();
    }

    [Fact]
    public void GeneratesScript_WithClassAndBlock()
    {
        var runResult = RunGenerator();

        var scriptFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Script.g.cs"));
        var scriptText = scriptFile.GetText().ToString();

        Assert.Contains("public class Script", scriptText);
        Assert.Contains("public ScriptBlock Block { get; set; }", scriptText);
    }

    [Fact]
    public void GeneratesText_WithClassAndBlock()
    {
        var runResult = RunGenerator();

        var textFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Text.g.cs"));
        var textText = textFile.GetText().ToString();

        Assert.Contains("public class Text", textText);
        Assert.Contains("public ScriptBlock Block { get; set; }", textText);
    }
}
