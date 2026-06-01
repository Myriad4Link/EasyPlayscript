using System.Linq;
using EasyPlayscript.Parsing;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptContentTests
{
    // ─── Phase 3: Parser AST Tests ────────────────────────────────────────────

    [Fact]
    public void SingleLine_OneParagraph()
    {
        var input = "Hello world";
        var (parser, errors) = PlayscriptContentHelper.Parse(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        Assert.Single(tree.page());
        Assert.Single(tree.page(0).paragraph());
        Assert.Single(tree.page(0).paragraph(0).line());
        var texts = tree.page(0).paragraph(0).line(0).TEXT();
        Assert.Single(texts);
        Assert.Equal("Hello world", texts[0].GetText());
    }

    [Fact]
    public void TwoLines_SameParagraph()
    {
        var input = "line 1\nline 2";
        var (parser, errors) = PlayscriptContentHelper.Parse(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        Assert.Single(tree.page());
        Assert.Single(tree.page(0).paragraph());
        Assert.Equal(2, tree.page(0).paragraph(0).line().Length);
        Assert.Equal("line 1", tree.page(0).paragraph(0).line(0).TEXT()[0].GetText());
        Assert.Equal("line 2", tree.page(0).paragraph(0).line(1).TEXT()[0].GetText());
    }

    [Fact]
    public void BlankLine_SeparatesParagraphs()
    {
        var input = "para 1\n\npara 2";
        var (parser, errors) = PlayscriptContentHelper.Parse(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        Assert.Single(tree.page());
        Assert.Equal(2, tree.page(0).paragraph().Length);
        Assert.Equal("para 1", tree.page(0).paragraph(0).line(0).TEXT()[0].GetText());
        Assert.Equal("para 2", tree.page(0).paragraph(1).line(0).TEXT()[0].GetText());
    }

    [Fact]
    public void SlashOnOwnLine_SeparatesPages()
    {
        var input = "page 1\n/\npage 2";
        var (parser, errors) = PlayscriptContentHelper.Parse(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        Assert.Equal(2, tree.page().Length);
        Assert.Equal("page 1", tree.page(0).paragraph(0).line(0).TEXT()[0].GetText());
        Assert.Equal("page 2", tree.page(1).paragraph(0).line(0).TEXT()[0].GetText());
    }

    [Fact]
    public void SlashAtEndOfLine_SeparatesPages()
    {
        var input = "page 1 /\npage 2";
        var (parser, errors) = PlayscriptContentHelper.Parse(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        Assert.Equal(2, tree.page().Length);
        var page1Texts = tree.page(0).paragraph(0).line(0).TEXT();
        Assert.Single(page1Texts);
        Assert.Equal("page 1 ", page1Texts[0].GetText());
    }

    [Fact]
    public void SlashWithBlankLines_SeparatesPages()
    {
        var input = "page 1\n\n/\n\npage 2";
        var (parser, errors) = PlayscriptContentHelper.Parse(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        Assert.Equal(2, tree.page().Length);
    }

    [Fact]
    public void ConsumerCall_MixedWithText()
    {
        var input = "Hello @transition(\"fade_out\") world";
        var (parser, errors) = PlayscriptContentHelper.Parse(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        var line = tree.page(0).paragraph(0).line(0);
        // Should have: TEXT, consumerCall, TEXT
        Assert.Equal(2, line.TEXT().Length);
        Assert.Single(line.consumerCall());
        Assert.Equal("Hello ", line.TEXT()[0].GetText());
        Assert.Equal(" world", line.TEXT()[1].GetText());
        Assert.Equal("transition", line.consumerCall(0).IDENTIFIER().GetText());
        Assert.Equal("\"fade_out\"", line.consumerCall(0).STRING_LITERAL().GetText());
    }

    [Fact]
    public void ConsumerCall_Standalone()
    {
        var input = "@transition(\"fade_out\")";
        var (parser, errors) = PlayscriptContentHelper.Parse(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        var line = tree.page(0).paragraph(0).line(0);
        Assert.Empty(line.TEXT());
        Assert.Single(line.consumerCall());
        Assert.Equal("transition", line.consumerCall(0).IDENTIFIER().GetText());
        Assert.Equal("\"fade_out\"", line.consumerCall(0).STRING_LITERAL().GetText());
    }

    [Fact]
    public void MultiplePages_WithParagraphs()
    {
        const string input = "p1l1\np1l2\n\np2l1\n/\np3l1";
        var (parser, errors) = PlayscriptContentHelper.Parse(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        // Page 1: 2 paragraphs (p1l1+p1l2, p2l1)
        // Page 2: 1 paragraph (p3l1)
        Assert.Equal(2, tree.page().Length);
        Assert.Equal(2, tree.page(0).paragraph().Length);
        Assert.Single(tree.page(1).paragraph());
    }

    // ─── Phase 4: Builder Tests ───────────────────────────────────────────────

    private static ScriptBlock BuildScriptBlock(string input)
    {
        var (parser, errors) = PlayscriptContentHelper.Parse(input);
        Assert.Empty(errors);
        var builder = new PlayscriptCodeBuilder();
        builder.BuildFromContent(parser.scriptContent());
        return builder.ContentResult;
    }

    [Fact]
    public void Builder_SingleLine_ProducesCorrectScriptBlock()
    {
        var block = BuildScriptBlock("Hello world");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines[0].Items);
        Assert.IsType<TextItem>(block.Pages[0].Paragraphs[0].Lines[0].Items[0]);
        Assert.Equal("Hello world", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
    }

    [Fact]
    public void Builder_TwoLines_SameParagraph()
    {
        var block = BuildScriptBlock("line 1\nline 2");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Equal(2, block.Pages[0].Paragraphs[0].Lines.Count);
        Assert.Equal("line 1", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
        Assert.Equal("line 2", ((TextItem)block.Pages[0].Paragraphs[0].Lines[1].Items[0]).Text);
    }

    [Fact]
    public void Builder_BlankLine_TwoParagraphs()
    {
        var block = BuildScriptBlock("para 1\n\npara 2");
        Assert.Single(block.Pages);
        Assert.Equal(2, block.Pages[0].Paragraphs.Count);
    }

    [Fact]
    public void Builder_PageBreak_TwoPages()
    {
        var block = BuildScriptBlock("page 1\n/\npage 2");
        Assert.Equal(2, block.Pages.Count);
        Assert.Equal("page 1", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
        Assert.Equal("page 2", ((TextItem)block.Pages[1].Paragraphs[0].Lines[0].Items[0]).Text);
    }

    [Fact]
    public void Builder_ConsumerCall_ProducesConsumerCallItem()
    {
        var block = BuildScriptBlock("@transition(\"fade_out\")");
        var items = block.Pages[0].Paragraphs[0].Lines[0].Items;
        Assert.Single(items);
        Assert.IsType<ConsumerCallItem>(items[0]);
        Assert.Equal("transition", ((ConsumerCallItem)items[0]).Identifier);
        Assert.Equal("fade_out", ((ConsumerCallItem)items[0]).Argument);
    }

    [Fact]
    public void Builder_NullContext_ProducesEmptyScriptBlock()
    {
        var builder = new PlayscriptCodeBuilder();
        builder.BuildFromContent(null);
        Assert.NotNull(builder.ContentResult);
        Assert.Empty(builder.ContentResult.Pages);
    }

    [Fact]
    public void Builder_MixedTextAndCall()
    {
        var block = BuildScriptBlock("Hello @transition(\"fade_out\") world");
        var items = block.Pages[0].Paragraphs[0].Lines[0].Items;
        Assert.Equal(3, items.Count);
        Assert.IsType<TextItem>(items[0]);
        Assert.IsType<ConsumerCallItem>(items[1]);
        Assert.IsType<TextItem>(items[2]);
        Assert.Equal("Hello ", ((TextItem)items[0]).Text);
        Assert.Equal("transition", ((ConsumerCallItem)items[1]).Identifier);
        Assert.Equal("fade_out", ((ConsumerCallItem)items[1]).Argument);
        Assert.Equal(" world", ((TextItem)items[2]).Text);
    }
}
