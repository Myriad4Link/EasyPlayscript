using System.Collections.Generic;
using EasyPlayscript.Parsing;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptStructureTests
{
    [Fact]
    public void ScriptWithBlock_ExtractsRawContent()
    {
        const string input = ".script(\"test\")[Hello world]";
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Single(result);
        Assert.Equal("test", result[0].Name);
        Assert.Equal("script", result[0].Identifier);
        Assert.Contains("Hello world", result[0].RawContent);
    }

    [Fact]
    public void TextWithBlock_ExtractsRawContent()
    {
        var input = ".text(\"intro\")[Welcome]";
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Single(result);
        Assert.Equal("text", result[0].Identifier);
        Assert.Equal("intro", result[0].Name);
    }

    [Fact]
    public void StandaloneCompilerCall_NoBlock()
    {
        var input = ".script(\"empty\")";
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Single(result);
        Assert.Null(result[0].RawContent);
    }

    [Fact]
    public void MultipleStatements_ExtractsAll()
    {
        var input = ".script(\"a\")[Hello] .text(\"b\")[World]";
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void BlockContentPreservesNewlines()
    {
        const string input = ".script(\"test\")[\nline 1\nline 2\n]";
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Contains("line 1", result[0].RawContent);
        Assert.Contains("line 2", result[0].RawContent);
    }

    [Fact]
    public void MalformedInput_ReportsErrors()
    {
        const string input = ".script(\"test\"][Hello]";
        var (_, errors) = PlayscriptStructureHelper.ParseStructureWithErrors(input);
        Assert.NotEmpty(errors);
    }

    // ─── End-to-End Tests ─────────────────────────────────────────────────────

    private static ScriptBlock ParseEndToEnd(string input)
    {
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Single(result);
        Assert.NotNull(result[0].RawContent);

        var trimmed = result[0].RawContent.Trim('\r', '\n');
        var (parser, errors) = PlayscriptContentHelper.Parse(trimmed);
        Assert.Empty(errors);

        var builder = new PlayscriptCodeBuilder();
        builder.BuildFromContent(parser.scriptContent());
        return builder.ContentResult;
    }

    [Fact]
    public void EndToEnd_NoNewlines()
    {
        var block = ParseEndToEnd(".script(\"t\")[Hello]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines[0].Items);
        Assert.Equal("Hello", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_LeadingNewline()
    {
        var block = ParseEndToEnd(".script(\"t\")[\nHello]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Equal("Hello", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_LeadingTwoNewlines()
    {
        var block = ParseEndToEnd(".script(\"t\")[\n\nHello]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Equal("Hello", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_TrailingNewline()
    {
        var block = ParseEndToEnd(".script(\"t\")[Hello\n]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Equal("Hello", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_TrailingTwoNewlines()
    {
        var block = ParseEndToEnd(".script(\"t\")[Hello\n\n]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Equal("Hello", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_BothSidesNewlines()
    {
        var block = ParseEndToEnd(".script(\"t\")[\nHello\n]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Equal("Hello", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_OnlyNewlines()
    {
        var block = ParseEndToEnd(".script(\"t\")[\n\n\n]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Empty(block.Pages[0].Paragraphs[0].Lines[0].Items);
    }

    [Fact]
    public void EndToEnd_TwoLines()
    {
        var block = ParseEndToEnd(".script(\"t\")[line 1\nline 2]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Equal(2, block.Pages[0].Paragraphs[0].Lines.Count);
        Assert.Equal("line 1", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
        Assert.Equal("line 2", ((TextItem)block.Pages[0].Paragraphs[0].Lines[1].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_TwoParagraphs()
    {
        var block = ParseEndToEnd(".script(\"t\")[para 1\n\npara 2]");
        Assert.Single(block.Pages);
        Assert.Equal(2, block.Pages[0].Paragraphs.Count);
        Assert.Equal("para 1", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
        Assert.Equal("para 2", ((TextItem)block.Pages[0].Paragraphs[1].Lines[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_PageBreak()
    {
        var block = ParseEndToEnd(".script(\"t\")[p1\n/\np2]");
        Assert.Equal(2, block.Pages.Count);
        Assert.Equal("p1", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
        Assert.Equal("p2", ((TextItem)block.Pages[1].Paragraphs[0].Lines[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_LeadingNewlineThenStructure()
    {
        var block = ParseEndToEnd(".script(\"t\")[\nline 1\nline 2]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Equal(2, block.Pages[0].Paragraphs[0].Lines.Count);
        Assert.Equal("line 1", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
        Assert.Equal("line 2", ((TextItem)block.Pages[0].Paragraphs[0].Lines[1].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_TrailingNewlineAfterStructure()
    {
        var block = ParseEndToEnd(".script(\"t\")[line 1\nline 2\n]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Equal(2, block.Pages[0].Paragraphs[0].Lines.Count);
        Assert.Equal("line 1", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
        Assert.Equal("line 2", ((TextItem)block.Pages[0].Paragraphs[0].Lines[1].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_LeadingTrailingWithPageBreak()
    {
        var block = ParseEndToEnd(".script(\"t\")[\np1\n/\np2\n]");
        Assert.Equal(2, block.Pages.Count);
        Assert.Equal("p1", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
        Assert.Equal("p2", ((TextItem)block.Pages[1].Paragraphs[0].Lines[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_TextBlock()
    {
        var block = ParseEndToEnd(".text(\"intro\")[Welcome]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Equal("Welcome", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
    }
}
