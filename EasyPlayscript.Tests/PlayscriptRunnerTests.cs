using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptRunnerTests
{
    [Fact]
    public void GeneratesScriptBlockAndPlayscriptRunner()
    {
        var generator = new PlayscriptRunner();
        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(nameof(PlayscriptRunnerTests),
            [CSharpSyntaxTree.ParseText("")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        var runResult = driver.RunGenerators(compilation).GetRunResult();

        var scriptBlockFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("ScriptBlock.g.cs"));
        var runnerFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("PlayscriptRunner.g.cs"));

        var scriptBlockText = scriptBlockFile.GetText().ToString();
        Assert.Contains("public class ScriptBlock", scriptBlockText);

        var runnerText = runnerFile.GetText().ToString();
        Assert.Contains("public class PlayscriptRunner", runnerText);
        Assert.Contains("public void Script(string name, ScriptBlock scriptBlock)", runnerText);
    }
}
