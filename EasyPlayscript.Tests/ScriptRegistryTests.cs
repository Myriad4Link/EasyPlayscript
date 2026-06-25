using System.Linq;
using EasyPlayscript.Generator;
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
        Assert.DoesNotContain("Dispatch =", scriptText);
    }

    [Fact]
    public void GeneratesText_WithClassAndBlock()
    {
        var runResult = RunGenerator();

        var textFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Text.g.cs"));
        var textText = textFile.GetText().ToString();

        Assert.Contains("public class Text", textText);
        Assert.Contains("public TextBlock Block { get; set; }", textText);
        Assert.DoesNotContain("Dispatch =", textText);
    }

    [Fact]
    public void ScriptBlock_HasPages()
    {
        var block = new ScriptBlock();
        Assert.NotNull(block.Pages);
        Assert.Empty(block.Pages);
    }

    [Fact]
    public void Page_HasParagraphs()
    {
        var page = new Page();
        Assert.NotNull(page.Paragraphs);
        Assert.Empty(page.Paragraphs);
    }

    [Fact]
    public void Paragraph_HasLines()
    {
        var paragraph = new Paragraph();
        Assert.NotNull(paragraph.Lines);
        Assert.Empty(paragraph.Lines);
    }

    [Fact]
    public void Line_HasItems()
    {
        var line = new Line();
        Assert.NotNull(line.Items);
        Assert.Empty(line.Items);
    }

    [Fact]
    public void TextItem_StoresText()
    {
        var item = new TextItem("Hello world");
        Assert.Equal("Hello world", item.Text);
    }

    [Fact]
    public void ConsumerCallItem_StoresIdentifierAndArguments()
    {
        var item = new ConsumerCallItem("transition", new System.Collections.Generic.List<ArgumentValue> { new StringArgument("fade_out") });
        Assert.Equal("transition", item.Identifier);
        Assert.Single(item.Arguments);
        Assert.IsType<StringArgument>(item.Arguments[0]);
        Assert.Equal("fade_out", ((StringArgument)item.Arguments[0]).Value);
    }

    [Fact]
    public void ConsumerCallItem_Result_DefaultsToNull()
    {
        var item = new ConsumerCallItem("test", new System.Collections.Generic.List<ArgumentValue>());
        Assert.Null(item.Result);
    }

    [Fact]
    public void ConsumerCallItem_CanStoreResult()
    {
        var item = new ConsumerCallItem("get_name", new System.Collections.Generic.List<ArgumentValue>());
        item.Result = "Player";
        Assert.Equal("Player", item.Result);
    }
}
