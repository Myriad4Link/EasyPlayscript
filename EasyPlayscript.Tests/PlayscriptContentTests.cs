using System;
using EasyPlayscript.DataModel;
using EasyPlayscript.Parsing;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptContentTests
{
    // ─── Phase 3: Parser AST Tests ────────────────────────────────────────────

    [Fact]
    public void SingleLine_OneParagraph()
    {
        const string input = "Hello world";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        Assert.Single(tree.page());
        Assert.Single(tree.page(0).paragraph());
        Assert.Single(tree.page(0).paragraph(0).line());
        var texts = tree.page(0).paragraph(0).line(0).segment(0).TEXT();
        Assert.Single(texts);
        Assert.Equal("Hello world", texts[0].GetText());
    }

    [Fact]
    public void TwoLines_SameParagraph()
    {
        const string input = "line 1\nline 2";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        Assert.Single(tree.page());
        Assert.Single(tree.page(0).paragraph());
        Assert.Equal(2, tree.page(0).paragraph(0).line().Length);
        Assert.Equal("line 1", tree.page(0).paragraph(0).line(0).segment(0).TEXT()[0].GetText());
        Assert.Equal("line 2", tree.page(0).paragraph(0).line(1).segment(0).TEXT()[0].GetText());
    }

    [Fact]
    public void BlankLine_SeparatesParagraphs()
    {
        const string input = "para 1\n\npara 2";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        Assert.Single(tree.page());
        Assert.Equal(2, tree.page(0).paragraph().Length);
        Assert.Equal("para 1", tree.page(0).paragraph(0).line(0).segment(0).TEXT()[0].GetText());
        Assert.Equal("para 2", tree.page(0).paragraph(1).line(0).segment(0).TEXT()[0].GetText());
    }

    [Fact]
    public void SlashOnOwnLine_SeparatesPages()
    {
        const string input = "page 1\n/\npage 2";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        Assert.Equal(2, tree.page().Length);
        Assert.Equal("page 1", tree.page(0).paragraph(0).line(0).segment(0).TEXT()[0].GetText());
        Assert.Equal("page 2", tree.page(1).paragraph(0).line(0).segment(0).TEXT()[0].GetText());
    }

    [Fact]
    public void SlashAtEndOfLine_SeparatesPages()
    {
        const string input = "page 1 /\npage 2";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        Assert.Equal(2, tree.page().Length);
        var page1Texts = tree.page(0).paragraph(0).line(0).segment(0).TEXT();
        Assert.Single(page1Texts);
        Assert.Equal("page 1 ", page1Texts[0].GetText());
    }

    [Fact]
    public void SlashWithBlankLines_SeparatesPages()
    {
        const string input = "page 1\n\n/\n\npage 2";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        Assert.Equal(2, tree.page().Length);
    }

    [Fact]
    public void ConsumerCall_MixedWithText()
    {
        const string input = "Hello @transition(\"fade_out\") world";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        var segment = tree.page(0).paragraph(0).line(0).segment(0);
        // Should have: TEXT, consumerCall, TEXT
        Assert.Equal(2, segment.TEXT().Length);
        Assert.Single(segment.consumerCall());
        Assert.Equal("Hello ", segment.TEXT()[0].GetText());
        Assert.Equal(" world", segment.TEXT()[1].GetText());
        Assert.Equal("transition", segment.consumerCall(0).IDENTIFIER().GetText());
        Assert.Single(segment.consumerCall(0).argument());
        Assert.NotNull(segment.consumerCall(0).argument(0).STRING_LITERAL());
        Assert.Equal("\"fade_out\"", segment.consumerCall(0).argument(0).STRING_LITERAL().GetText());
    }

    [Fact]
    public void ConsumerCall_Standalone()
    {
        const string input = "@transition(\"fade_out\")";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        var segment = tree.page(0).paragraph(0).line(0).segment(0);
        Assert.Empty(segment.TEXT());
        Assert.Single(segment.consumerCall());
        Assert.Equal("transition", segment.consumerCall(0).IDENTIFIER().GetText());
        Assert.Single(segment.consumerCall(0).argument());
        Assert.NotNull(segment.consumerCall(0).argument(0).STRING_LITERAL());
        Assert.Equal("\"fade_out\"", segment.consumerCall(0).argument(0).STRING_LITERAL().GetText());
    }

    // ─── Line Segment Parser Tests ─────────────────────────────────────────

    [Fact]
    public void LineSegments_PlusInline_ParsesAsTwoSegments()
    {
        const string input = "Hello+World";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        Assert.Single(tree.page());
        Assert.Single(tree.page(0).paragraph());
        var lines = tree.page(0).paragraph(0).line();
        Assert.Single(lines);

        Assert.Single(lines[0].PLUS());
        Assert.Equal(2, lines[0].segment().Length);
        Assert.Single(lines[0].segment(0).TEXT());
        Assert.Equal("Hello", lines[0].segment(0).TEXT()[0].GetText());
        Assert.Single(lines[0].segment(1).TEXT());
        Assert.Equal("World", lines[0].segment(1).TEXT()[0].GetText());
    }

    [Fact]
    public void LineSegments_NoPlus_SingleSegment()
    {
        const string input = "Hello\nWorld";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        var lines = tree.page(0).paragraph(0).line();
        Assert.Equal(2, lines.Length);
        Assert.Empty(lines[0].PLUS());
        Assert.Empty(lines[1].PLUS());
    }

    [Fact]
    public void LineSegments_EscapedPlus_IsSingleSegment()
    {
        const string input = @"a\+b";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        var line = tree.page(0).paragraph(0).line(0);
        Assert.Empty(line.PLUS());
        Assert.Single(line.segment());
        Assert.Single(line.segment(0).TEXT());
        Assert.Equal(@"a\+b", line.segment(0).TEXT()[0].GetText());
    }

    [Fact]
    public void LineSegments_PlusOnlyLine_ReportsParserError()
    {
        const string input = "Hello\n+";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        parser.scriptContent();

        Assert.NotEmpty(errors);
    }

    // ─── Multi-param Consumer Call Tests ─────────────────────────────────────

    [Fact]
    public void ConsumerCall_NoParams_Parses()
    {
        const string input = "@func()";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        var segment = tree.page(0).paragraph(0).line(0).segment(0);
        Assert.Empty(segment.TEXT());
        Assert.Single(segment.consumerCall());
        Assert.Equal("func", segment.consumerCall(0).IDENTIFIER().GetText());
        Assert.Empty(segment.consumerCall(0).argument());
    }

    [Fact]
    public void ConsumerCall_MultipleParams_Parses()
    {
        const string input = "@func(\"a\", \"b\", \"c\")";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        var segment = tree.page(0).paragraph(0).line(0).segment(0);
        Assert.Single(segment.consumerCall());
        Assert.Equal("func", segment.consumerCall(0).IDENTIFIER().GetText());
        Assert.Equal(3, segment.consumerCall(0).argument().Length);
        Assert.NotNull(segment.consumerCall(0).argument(0).STRING_LITERAL());
        Assert.Equal("\"a\"", segment.consumerCall(0).argument(0).STRING_LITERAL().GetText());
        Assert.NotNull(segment.consumerCall(0).argument(1).STRING_LITERAL());
        Assert.Equal("\"b\"", segment.consumerCall(0).argument(1).STRING_LITERAL().GetText());
        Assert.NotNull(segment.consumerCall(0).argument(2).STRING_LITERAL());
        Assert.Equal("\"c\"", segment.consumerCall(0).argument(2).STRING_LITERAL().GetText());
    }

    [Fact]
    public void Builder_ConsumerCall_NoParams_ProducesConsumerCallItem()
    {
        var block = BuildScriptBlock("@func()");
        var items = block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items;
        Assert.Single(items);
        Assert.IsType<ConsumerCallItem>(items[0]);
        Assert.Equal("func", ((ConsumerCallItem)items[0]).Identifier);
        Assert.Empty(((ConsumerCallItem)items[0]).Arguments);
    }

    [Fact]
    public void Builder_ConsumerCall_MultipleParams()
    {
        var block = BuildScriptBlock("@func(\"a\", \"b\")");
        var items = block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items;
        Assert.Single(items);
        Assert.IsType<ConsumerCallItem>(items[0]);
        Assert.Equal("func", ((ConsumerCallItem)items[0]).Identifier);
        Assert.Equal(2, ((ConsumerCallItem)items[0]).Arguments.Count);
        Assert.IsType<StringArgument>(((ConsumerCallItem)items[0]).Arguments[0]);
        Assert.Equal("a", ((StringArgument)((ConsumerCallItem)items[0]).Arguments[0]).Value);
        Assert.IsType<StringArgument>(((ConsumerCallItem)items[0]).Arguments[1]);
        Assert.Equal("b", ((StringArgument)((ConsumerCallItem)items[0]).Arguments[1]).Value);
    }

    // ─── Typed Parameter Tests ────────────────────────────────────────────────

    [Fact]
    public void ConsumerCall_IntegerParam_Parses()
    {
        const string input = "@func(42)";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        var call = tree.page(0).paragraph(0).line(0).segment(0).consumerCall(0);
        Assert.Equal("func", call.IDENTIFIER().GetText());
        Assert.Single(call.argument());
        Assert.NotNull(call.argument(0).INTEGER_LITERAL());
        Assert.Equal("42", call.argument(0).INTEGER_LITERAL().GetText());
    }

    [Fact]
    public void ConsumerCall_NegativeInteger_Parses()
    {
        const string input = "@func(-3)";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        var call = tree.page(0).paragraph(0).line(0).segment(0).consumerCall(0);
        Assert.Single(call.argument());
        Assert.NotNull(call.argument(0).INTEGER_LITERAL());
        Assert.Equal("-3", call.argument(0).INTEGER_LITERAL().GetText());
    }

    [Fact]
    public void ConsumerCall_FloatParam_Parses()
    {
        const string input = "@func(3.14)";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        var call = tree.page(0).paragraph(0).line(0).segment(0).consumerCall(0);
        Assert.Single(call.argument());
        Assert.NotNull(call.argument(0).FLOAT_LITERAL());
        Assert.Equal("3.14", call.argument(0).FLOAT_LITERAL().GetText());
    }

    [Fact]
    public void ConsumerCall_NegativeFloat_Parses()
    {
        const string input = "@func(-0.5)";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        var call = tree.page(0).paragraph(0).line(0).segment(0).consumerCall(0);
        Assert.Single(call.argument());
        Assert.NotNull(call.argument(0).FLOAT_LITERAL());
        Assert.Equal("-0.5", call.argument(0).FLOAT_LITERAL().GetText());
    }

    [Fact]
    public void ConsumerCall_BoolParam_Parses()
    {
        const string input = "@func(true)";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        var call = tree.page(0).paragraph(0).line(0).segment(0).consumerCall(0);
        Assert.Single(call.argument());
        Assert.NotNull(call.argument(0).BOOLEAN_LITERAL());
        Assert.Equal("true", call.argument(0).BOOLEAN_LITERAL().GetText());
    }

    [Fact]
    public void ConsumerCall_MixedTypes_Parses()
    {
        const string input = "@func(\"str\", 42, 3.14, true)";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        var call = tree.page(0).paragraph(0).line(0).segment(0).consumerCall(0);
        Assert.Equal(4, call.argument().Length);
        Assert.NotNull(call.argument(0).STRING_LITERAL());
        Assert.NotNull(call.argument(1).INTEGER_LITERAL());
        Assert.NotNull(call.argument(2).FLOAT_LITERAL());
        Assert.NotNull(call.argument(3).BOOLEAN_LITERAL());
    }

    [Fact]
    public void Builder_IntegerParam()
    {
        var block = BuildScriptBlock("@func(42)");
        var item = (ConsumerCallItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0];
        Assert.Single(item.Arguments);
        Assert.IsType<IntArgument>(item.Arguments[0]);
        Assert.Equal(42, ((IntArgument)item.Arguments[0]).Value);
    }

    [Fact]
    public void Builder_NegativeInteger()
    {
        var block = BuildScriptBlock("@func(-3)");
        var item = (ConsumerCallItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0];
        Assert.Single(item.Arguments);
        Assert.IsType<IntArgument>(item.Arguments[0]);
        Assert.Equal(-3, ((IntArgument)item.Arguments[0]).Value);
    }

    [Fact]
    public void Builder_FloatParam()
    {
        var block = BuildScriptBlock("@func(3.14)");
        var item = (ConsumerCallItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0];
        Assert.Single(item.Arguments);
        Assert.IsType<DoubleArgument>(item.Arguments[0]);
        Assert.Equal(3.14, ((DoubleArgument)item.Arguments[0]).Value);
    }

    [Fact]
    public void Builder_NegativeFloat()
    {
        var block = BuildScriptBlock("@func(-0.5)");
        var item = (ConsumerCallItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0];
        Assert.Single(item.Arguments);
        Assert.IsType<DoubleArgument>(item.Arguments[0]);
        Assert.Equal(-0.5, ((DoubleArgument)item.Arguments[0]).Value);
    }

    [Fact]
    public void Builder_BoolParam()
    {
        var block = BuildScriptBlock("@func(true)");
        var item = (ConsumerCallItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0];
        Assert.Single(item.Arguments);
        Assert.IsType<BoolArgument>(item.Arguments[0]);
        Assert.True(((BoolArgument)item.Arguments[0]).Value);
    }

    [Fact]
    public void Builder_MixedTypes()
    {
        var block = BuildScriptBlock("@func(\"str\", 42, 3.14, true)");
        var item = (ConsumerCallItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0];
        Assert.Equal(4, item.Arguments.Count);
        Assert.IsType<StringArgument>(item.Arguments[0]);
        Assert.Equal("str", ((StringArgument)item.Arguments[0]).Value);
        Assert.IsType<IntArgument>(item.Arguments[1]);
        Assert.Equal(42, ((IntArgument)item.Arguments[1]).Value);
        Assert.IsType<DoubleArgument>(item.Arguments[2]);
        Assert.Equal(3.14, ((DoubleArgument)item.Arguments[2]).Value);
        Assert.IsType<BoolArgument>(item.Arguments[3]);
        Assert.True(((BoolArgument)item.Arguments[3]).Value);
    }

    [Fact]
    public void Builder_StringArgumentWithEscapes_Unescapes()
    {
        var block = BuildScriptBlock("@func(\"He said \\\"hello\\\"\")");
        var item = (ConsumerCallItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0];
        Assert.Single(item.Arguments);
        Assert.IsType<StringArgument>(item.Arguments[0]);
        Assert.Equal("He said \"hello\"", ((StringArgument)item.Arguments[0]).Value);
    }

    [Fact]
    public void Builder_IntegerOverflow_ReportsError()
    {
        var (parser, errors) = PlayscriptContentHelper.ParseScript("@func(9999999999)");
        Assert.Empty(errors);
        var builder = new PlayscriptCodeBuilder();
        builder.BuildScriptFromContent(parser.scriptContent());
        Assert.NotEmpty(builder.Errors);
        Assert.Contains("out of range", builder.Errors[0].Msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Builder_FloatOverflow_ReportsError()
    {
        var (parser, errors) = PlayscriptContentHelper.ParseScript("@func(1e999)");
        Assert.Empty(errors);
        var builder = new PlayscriptCodeBuilder();
        builder.BuildScriptFromContent(parser.scriptContent());
        Assert.NotEmpty(builder.Errors);
        Assert.Contains("out of range", builder.Errors[0].Msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MultiplePages_WithParagraphs()
    {
        const string input = "p1l1\np1l2\n\np2l1\n/\np3l1";
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        var tree = parser.scriptContent();

        Assert.Empty(errors);
        // Page 1: 2 paragraphs (p1l1+p1l2, p2l1)
        // Page 2: 1 paragraph (p3l1)
        Assert.Equal(2, tree.page().Length);
        Assert.Equal(2, tree.page(0).paragraph().Length);
        Assert.Single(tree.page(1).paragraph());
    }

    // ─── textContent Parser Tests ─────────────────────────────────────────────

    [Fact]
    public void TextContent_SingleLine_OneParagraph()
    {
        const string input = "Hello world";
        var (parser, errors) = PlayscriptContentHelper.ParseText(input);
        var tree = parser.textContent();

        Assert.Empty(errors);
        Assert.Single(tree.textParagraph());
        Assert.Single(tree.textParagraph(0).textLine());
        var texts = tree.textParagraph(0).textLine(0).TEXT();
        Assert.Single(texts);
        Assert.Equal("Hello world", texts[0].GetText());
    }

    [Fact]
    public void TextContent_TwoLines_SameParagraph()
    {
        const string input = "line 1\nline 2";
        var (parser, errors) = PlayscriptContentHelper.ParseText(input);
        var tree = parser.textContent();

        Assert.Empty(errors);
        Assert.Single(tree.textParagraph());
        Assert.Equal(2, tree.textParagraph(0).textLine().Length);
        Assert.Equal("line 1", tree.textParagraph(0).textLine(0).TEXT()[0].GetText());
        Assert.Equal("line 2", tree.textParagraph(0).textLine(1).TEXT()[0].GetText());
    }

    [Fact]
    public void TextContent_BlankLine_TwoParagraphs()
    {
        const string input = "para 1\n\npara 2";
        var (parser, errors) = PlayscriptContentHelper.ParseText(input);
        var tree = parser.textContent();

        Assert.Empty(errors);
        Assert.Equal(2, tree.textParagraph().Length);
        Assert.Equal("para 1", tree.textParagraph(0).textLine(0).TEXT()[0].GetText());
        Assert.Equal("para 2", tree.textParagraph(1).textLine(0).TEXT()[0].GetText());
    }

    [Fact]
    public void TextContent_SlashIsLineContent()
    {
        const string input = "before\n/\nafter";
        var (parser, errors) = PlayscriptContentHelper.ParseText(input);
        var tree = parser.textContent();

        Assert.Empty(errors);
        Assert.Single(tree.textParagraph());
        Assert.Equal(3, tree.textParagraph(0).textLine().Length);
        Assert.Equal("before", tree.textParagraph(0).textLine(0).TEXT()[0].GetText());
        Assert.Single(tree.textParagraph(0).textLine(1).SLASH());
        Assert.Equal("/", tree.textParagraph(0).textLine(1).SLASH(0).GetText());
        Assert.Equal("after", tree.textParagraph(0).textLine(2).TEXT()[0].GetText());
    }

    [Fact]
    public void TextContent_SlashInlineWithText()
    {
        const string input = "price is 5/10";
        var (parser, errors) = PlayscriptContentHelper.ParseText(input);
        var tree = parser.textContent();

        Assert.Empty(errors);
        var line = tree.textParagraph(0).textLine(0);
        // Should have TEXT, SLASH, TEXT
        Assert.Equal(2, line.TEXT().Length);
        Assert.Single(line.SLASH());
        Assert.Equal("price is 5", line.TEXT()[0].GetText());
        Assert.Equal("/", line.SLASH(0).GetText());
        Assert.Equal("10", line.TEXT()[1].GetText());
    }

    [Fact]
    public void TextContent_ConsumerCall_MixedWithText()
    {
        const string input = "Hello @get_name() world";
        var (parser, errors) = PlayscriptContentHelper.ParseText(input);
        var tree = parser.textContent();

        Assert.Empty(errors);
        var line = tree.textParagraph(0).textLine(0);
        Assert.Equal(2, line.TEXT().Length);
        Assert.Single(line.consumerCall());
        Assert.Equal("Hello ", line.TEXT()[0].GetText());
        Assert.Equal(" world", line.TEXT()[1].GetText());
        Assert.Equal("get_name", line.consumerCall(0).IDENTIFIER().GetText());
    }

    [Fact]
    public void TextContent_EmptyInput_ReportsError()
    {
        const string input = "";
        var (parser, errors) = PlayscriptContentHelper.ParseText(input);
        parser.textContent();

        // Empty input produces a parser error (EOF unexpected)
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void TextContent_ThreeParagraphs_MixedContent()
    {
        const string input =
            "Hi!\n\nHow's your day, @get_name()?\nLook at this wonderful slash:\n/\nIt's great, isn't it?";
        var (parser, errors) = PlayscriptContentHelper.ParseText(input);
        var tree = parser.textContent();

        Assert.Empty(errors);
        Assert.Equal(2, tree.textParagraph().Length);

        // Paragraph 1: "Hi!"
        Assert.Single(tree.textParagraph(0).textLine());
        Assert.Equal("Hi!", tree.textParagraph(0).textLine(0).TEXT()[0].GetText());

        // Paragraph 2: 4 lines (single newlines within paragraph)
        Assert.Equal(4, tree.textParagraph(1).textLine().Length);
        Assert.Single(tree.textParagraph(1).textLine(0).consumerCall());
        Assert.Equal("get_name", tree.textParagraph(1).textLine(0).consumerCall(0).IDENTIFIER().GetText());
        Assert.Single(tree.textParagraph(1).textLine(2).SLASH());
    }

    // ─── Phase 4: Builder Tests ───────────────────────────────────────────────

    private static ScriptBlock BuildScriptBlock(string input)
    {
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        Assert.Empty(errors);
        var builder = new PlayscriptCodeBuilder();
        builder.BuildScriptFromContent(parser.scriptContent());
        return builder.ContentResult;
    }

    [Fact]
    public void Builder_SingleLine_ProducesCorrectScriptBlock()
    {
        var block = BuildScriptBlock("Hello world");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items);
        Assert.IsType<TextItem>(block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]);
        Assert.Equal("Hello world", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Builder_TwoLines_SameParagraph()
    {
        var block = BuildScriptBlock("line 1\nline 2");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Equal(2, block.Pages[0].Paragraphs[0].Lines.Count);
        Assert.Equal("line 1", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
        Assert.Equal("line 2", ((TextItem)block.Pages[0].Paragraphs[0].Lines[1].Segments[0].Items[0]).Text);
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
        Assert.Equal("page 1", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
        Assert.Equal("page 2", ((TextItem)block.Pages[1].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Builder_ConsumerCall_ProducesConsumerCallItem()
    {
        var block = BuildScriptBlock("@transition(\"fade_out\")");
        var items = block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items;
        Assert.Single(items);
        Assert.IsType<ConsumerCallItem>(items[0]);
        Assert.Equal("transition", ((ConsumerCallItem)items[0]).Identifier);
        Assert.Single(((ConsumerCallItem)items[0]).Arguments);
        Assert.IsType<StringArgument>(((ConsumerCallItem)items[0]).Arguments[0]);
        Assert.Equal("fade_out", ((StringArgument)((ConsumerCallItem)items[0]).Arguments[0]).Value);
    }

    [Fact]
    public void Builder_NullContext_ProducesEmptyScriptBlock()
    {
        var builder = new PlayscriptCodeBuilder();
        builder.BuildScriptFromContent(null);
        Assert.NotNull(builder.ContentResult);
        Assert.Empty(builder.ContentResult.Pages);
    }

    [Fact]
    public void Builder_MixedTextAndCall()
    {
        var block = BuildScriptBlock("Hello @transition(\"fade_out\") world");
        var items = block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items;
        Assert.Equal(3, items.Count);
        Assert.IsType<TextItem>(items[0]);
        Assert.IsType<ConsumerCallItem>(items[1]);
        Assert.IsType<TextItem>(items[2]);
        Assert.Equal("Hello ", ((TextItem)items[0]).Text);
        Assert.Equal("transition", ((ConsumerCallItem)items[1]).Identifier);
        Assert.Single(((ConsumerCallItem)items[1]).Arguments);
        Assert.IsType<StringArgument>(((ConsumerCallItem)items[1]).Arguments[0]);
        Assert.Equal("fade_out", ((StringArgument)((ConsumerCallItem)items[1]).Arguments[0]).Value);
        Assert.Equal(" world", ((TextItem)items[2]).Text);
    }

    // ─── Line Segment Builder Tests ─────────────────────────────────────────

    [Fact]
    public void Builder_LineSegments_TwoSegmentsFromPlus()
    {
        var block = BuildScriptBlock("Hello+World");
        var lines = block.Pages[0].Paragraphs[0].Lines;
        Assert.Single(lines);
        Assert.Equal(2, lines[0].Segments.Count);
        Assert.Equal("Hello", ((TextItem)lines[0].Segments[0].Items[0]).Text);
        Assert.Equal("World", ((TextItem)lines[0].Segments[1].Items[0]).Text);
    }

    [Fact]
    public void Builder_LineSegments_ThreeSegments()
    {
        var block = BuildScriptBlock("A+B+C");
        var lines = block.Pages[0].Paragraphs[0].Lines;
        Assert.Single(lines);
        Assert.Equal(3, lines[0].Segments.Count);
        Assert.Equal("A", ((TextItem)lines[0].Segments[0].Items[0]).Text);
        Assert.Equal("B", ((TextItem)lines[0].Segments[1].Items[0]).Text);
        Assert.Equal("C", ((TextItem)lines[0].Segments[2].Items[0]).Text);
    }

    [Fact]
    public void Builder_LineSegments_EscapedPlus_IsSingleSegment()
    {
        var block = BuildScriptBlock(@"a\+b");
        var lines = block.Pages[0].Paragraphs[0].Lines;
        Assert.Single(lines);
        Assert.Single(lines[0].Segments);
        Assert.Equal("a+b", ((TextItem)lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Builder_LineSegments_WithConsumerCall()
    {
        var block = BuildScriptBlock("Hello+@get_name()");
        var lines = block.Pages[0].Paragraphs[0].Lines;
        Assert.Single(lines);
        Assert.Equal(2, lines[0].Segments.Count);
        Assert.Equal("Hello", ((TextItem)lines[0].Segments[0].Items[0]).Text);
        Assert.Single(lines[0].Segments[1].Items);
        Assert.IsType<ConsumerCallItem>(lines[0].Segments[1].Items[0]);
        Assert.Equal("get_name", ((ConsumerCallItem)lines[0].Segments[1].Items[0]).Identifier);
    }

    [Fact]
    public void Builder_LineSegments_SegmentWithMultipleItems()
    {
        var block = BuildScriptBlock("Hi @get_name()+World");
        var lines = block.Pages[0].Paragraphs[0].Lines;
        Assert.Single(lines);
        Assert.Equal(2, lines[0].Segments.Count);
        Assert.Equal(2, lines[0].Segments[0].Items.Count);
        Assert.IsType<TextItem>(lines[0].Segments[0].Items[0]);
        Assert.IsType<ConsumerCallItem>(lines[0].Segments[0].Items[1]);
        Assert.Single(lines[0].Segments[1].Items);
        Assert.Equal("World", ((TextItem)lines[0].Segments[1].Items[0]).Text);
    }

    [Fact]
    public void Builder_LineSegments_MultipleLinesWithDifferentSegmentCounts()
    {
        var block = BuildScriptBlock("A+B\nC");
        var lines = block.Pages[0].Paragraphs[0].Lines;
        Assert.Equal(2, lines.Count);
        Assert.Equal(2, lines[0].Segments.Count);
        Assert.Single(lines[1].Segments);
    }

    // ─── Step 5: Location Tracking ────────────────────────────────────────────

    [Fact]
    public void Builder_ConsumerCall_StoresLineAndColumn()
    {
        var block = BuildScriptBlock("@transition(\"fade_out\")");
        var item = (ConsumerCallItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0];
        Assert.True(item.Line > 0);
        Assert.True(item.Col >= 0);
    }

    // ─── Phase 3: TextBlock Builder Tests ─────────────────────────────────────

    private static TextBlock BuildTextBlock(string input)
    {
        var (parser, errors) = PlayscriptContentHelper.ParseText(input);
        Assert.Empty(errors);
        var builder = new PlayscriptCodeBuilder();
        builder.BuildTextFromContent(parser.textContent());
        return builder.TextResult;
    }

    [Fact]
    public void Builder_TextBlock_SingleLine()
    {
        var block = BuildTextBlock("Hello world");
        Assert.Single(block.Lines);
        Assert.Single(block.Lines[0].Segments[0].Items);
        Assert.IsType<TextItem>(block.Lines[0].Segments[0].Items[0]);
        Assert.Equal("Hello world", ((TextItem)block.Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Builder_TextBlock_TwoLines_SameParagraph()
    {
        var block = BuildTextBlock("line 1\nline 2");
        Assert.Equal(2, block.Lines.Count);
        Assert.Equal("line 1", ((TextItem)block.Lines[0].Segments[0].Items[0]).Text);
        Assert.Equal("line 2", ((TextItem)block.Lines[1].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Builder_TextBlock_BlankLine_Preserved()
    {
        var block = BuildTextBlock("para 1\n\npara 2");
        Assert.Equal(3, block.Lines.Count);
        Assert.Equal("para 1", ((TextItem)block.Lines[0].Segments[0].Items[0]).Text);
        Assert.Empty(block.Lines[1].Segments);
        Assert.Equal("para 2", ((TextItem)block.Lines[2].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Builder_TextBlock_SlashIsLineContent()
    {
        var block = BuildTextBlock("before\n/\nafter");
        Assert.Equal(3, block.Lines.Count);
        Assert.Equal("before", ((TextItem)block.Lines[0].Segments[0].Items[0]).Text);
        Assert.Single(block.Lines[1].Segments[0].Items);
        Assert.IsType<TextItem>(block.Lines[1].Segments[0].Items[0]);
        Assert.Equal("/", ((TextItem)block.Lines[1].Segments[0].Items[0]).Text);
        Assert.Equal("after", ((TextItem)block.Lines[2].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Builder_TextBlock_InlineSlash()
    {
        var block = BuildTextBlock("price is 5/10");
        Assert.Single(block.Lines);
        var items = block.Lines[0].Segments[0].Items;
        Assert.Equal(3, items.Count);
        Assert.Equal("price is 5", ((TextItem)items[0]).Text);
        Assert.Equal("/", ((TextItem)items[1]).Text);
        Assert.Equal("10", ((TextItem)items[2]).Text);
    }

    [Fact]
    public void Builder_TextBlock_InlineConsumerCall()
    {
        var block = BuildTextBlock("Hi, @get_name().");
        Assert.Single(block.Lines);
        var items = block.Lines[0].Segments[0].Items;
        Assert.Equal(3, items.Count);
        Assert.IsType<TextItem>(items[0]);
        Assert.Equal("Hi, ", ((TextItem)items[0]).Text);
        Assert.IsType<ConsumerCallItem>(items[1]);
        Assert.Equal("get_name", ((ConsumerCallItem)items[1]).Identifier);
        Assert.IsType<TextItem>(items[2]);
        Assert.Equal(".", ((TextItem)items[2]).Text);
    }

    [Fact]
    public void Builder_TextBlock_EmptyInput()
    {
        var block = BuildTextBlock("");
        Assert.Empty(block.Lines);
    }

    [Fact]
    public void Builder_TextBlock_MultipleParagraphsWithCalls()
    {
        var block = BuildTextBlock("Hello @a()\n\nWorld @b(\"x\")");
        Assert.Equal(3, block.Lines.Count);
        Assert.Equal("Hello ", ((TextItem)block.Lines[0].Segments[0].Items[0]).Text);
        Assert.IsType<ConsumerCallItem>(block.Lines[0].Segments[0].Items[1]);
        Assert.Equal("a", ((ConsumerCallItem)block.Lines[0].Segments[0].Items[1]).Identifier);
        Assert.Empty(block.Lines[1].Segments);
        Assert.Equal("World ", ((TextItem)block.Lines[2].Segments[0].Items[0]).Text);
        Assert.IsType<ConsumerCallItem>(block.Lines[2].Segments[0].Items[1]);
        Assert.Equal("b", ((ConsumerCallItem)block.Lines[2].Segments[0].Items[1]).Identifier);
        Assert.Single(((ConsumerCallItem)block.Lines[2].Segments[0].Items[1]).Arguments);
        Assert.IsType<StringArgument>(((ConsumerCallItem)block.Lines[2].Segments[0].Items[1]).Arguments[0]);
        Assert.Equal("x", ((StringArgument)((ConsumerCallItem)block.Lines[2].Segments[0].Items[1]).Arguments[0]).Value);
    }

    [Fact]
    public void Builder_TextBlock_OnlyBlankLines()
    {
        var block = BuildTextBlock("\n\n\n");
        Assert.Empty(block.Lines);
    }

    [Fact]
    public void Builder_TextBlock_LeadingTrailingNewlines()
    {
        var block = BuildTextBlock("\nHello\n");
        Assert.Single(block.Lines);
        Assert.Equal("Hello", ((TextItem)block.Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Builder_TextBlock_ThreeParagraphs_MixedContent()
    {
        var block = BuildTextBlock(
            "Hi!\n\nHow's your day, @get_name()?\nLook at this wonderful slash:\n/\nIt's great, isn't it?");
        Assert.Equal(6, block.Lines.Count);
        Assert.Equal("Hi!", ((TextItem)block.Lines[0].Segments[0].Items[0]).Text);
        Assert.Empty(block.Lines[1].Segments);
        Assert.Equal(3, block.Lines[2].Segments[0].Items.Count);
        Assert.Equal("How's your day, ", ((TextItem)block.Lines[2].Segments[0].Items[0]).Text);
        Assert.Equal("get_name", ((ConsumerCallItem)block.Lines[2].Segments[0].Items[1]).Identifier);
        Assert.Equal("?", ((TextItem)block.Lines[2].Segments[0].Items[2]).Text);
        Assert.Equal("Look at this wonderful slash:", ((TextItem)block.Lines[3].Segments[0].Items[0]).Text);
        Assert.Equal("/", ((TextItem)block.Lines[4].Segments[0].Items[0]).Text);
        Assert.Equal("It's great, isn't it?", ((TextItem)block.Lines[5].Segments[0].Items[0]).Text);
    }

    // ─── Phase 5: Escape Characters ───────────────────────────────────────────

    [Fact]
    public void Escape_AtSign_InScript()
    {
        var block = BuildScriptBlock(@"hello \@world");
        var text = ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text;
        Assert.Equal("hello @world", text);
    }

    [Fact]
    public void Escape_AtSign_InText()
    {
        var block = BuildTextBlock(@"hello \@world");
        Assert.Single(block.Lines);
        Assert.Single(block.Lines[0].Segments[0].Items);
        Assert.Equal("hello @world", ((TextItem)block.Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Escape_Hash_InScript()
    {
        var block = BuildScriptBlock(@"cost is \#5");
        Assert.Equal("cost is #5", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Escape_Hash_InText()
    {
        var block = BuildTextBlock(@"cost is \#5");
        Assert.Equal("cost is #5", ((TextItem)block.Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Escape_Slash_InScript()
    {
        var block = BuildScriptBlock(@"a\/b");
        // Should be a single TextItem with "/" as literal text, not a page break
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items);
        Assert.Equal("a/b", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Escape_Slash_InText()
    {
        var block = BuildTextBlock(@"a\/b");
        Assert.Single(block.Lines);
        Assert.Single(block.Lines[0].Segments[0].Items);
        Assert.Equal("a/b", ((TextItem)block.Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Escape_Backslash_InScript()
    {
        var block = BuildScriptBlock(@"path\\to");
        Assert.Equal("path\\to", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Escape_Backslash_InText()
    {
        var block = BuildTextBlock(@"path\\to");
        Assert.Equal("path\\to", ((TextItem)block.Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Escape_Newline_InScript()
    {
        var block = BuildScriptBlock("before\\nafter");
        // \n escape should produce a single line with an embedded newline, not two lines
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Equal("before\nafter", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Escape_Newline_InText()
    {
        var block = BuildTextBlock("before\\nafter");
        Assert.Single(block.Lines);
        Assert.Equal("before\nafter", ((TextItem)block.Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Escape_Unknown_KeptAsIs()
    {
        var block = BuildScriptBlock(@"\a\b\c");
        Assert.Equal(@"\a\b\c", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Escape_MultipleEscapes()
    {
        var block = BuildScriptBlock(@"use \# \@ \/ \\");
        Assert.Equal(@"use # @ / \", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Escape_NoEscape_Unchanged()
    {
        var block = BuildScriptBlock("plain text");
        Assert.Equal("plain text", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void Escape_EscapedAtSign_PreventsConsumerCall()
    {
        var block = BuildScriptBlock(@"literal \@get_name()");
        var items = block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items;
        // Escaped @ should be literal text, NOT a consumer call
        Assert.Single(items);
        Assert.IsType<TextItem>(items[0]);
        Assert.Equal("literal @get_name()", ((TextItem)items[0]).Text);
    }
}