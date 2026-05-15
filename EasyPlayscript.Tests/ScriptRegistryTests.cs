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
    public void GeneratesScriptBlock_WithEmptyClass()
    {
        var runResult = RunGenerator();

        var scriptBlockFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("ScriptBlock.g.cs"));
        var scriptBlockText = scriptBlockFile.GetText().ToString();

        Assert.Contains("public class ScriptBlock", scriptBlockText);
    }

    [Fact]
    public void GeneratesScript_WithClassAndBlocksList()
    {
        var runResult = RunGenerator();

        var scriptFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Script.g.cs"));
        var scriptText = scriptFile.GetText().ToString();

        Assert.Contains("public class Script", scriptText);
        Assert.Contains("public List<ScriptBlock> Blocks", scriptText);
    }

    [Fact]
    public void GeneratesText_WithClassAndBlocksList()
    {
        var runResult = RunGenerator();

        var textFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Text.g.cs"));
        var textText = textFile.GetText().ToString();

        Assert.Contains("public class Text", textText);
        Assert.Contains("public List<ScriptBlock> Blocks", textText);
    }

    [Fact]
    public void GeneratesScriptRegistry_WithClassAndDictionary()
    {
        var runResult = RunGenerator();

        var registryFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("ScriptRegistry.g.cs"));
        var registryText = registryFile.GetText().ToString();

        Assert.Contains("public class ScriptRegistry", registryText);
        Assert.Contains("public Dictionary<string, Script> Scripts", registryText);
    }
}
