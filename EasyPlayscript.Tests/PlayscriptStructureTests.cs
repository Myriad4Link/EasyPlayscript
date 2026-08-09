using EasyPlayscript.DataModel;
using EasyPlayscript.Parsing;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptStructureTests
{
    [Fact]
    public void ScriptWithBlock_ExtractsRawContent()
    {
        const string input = "script test[Hello world]";
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Single(result.Results);
        Assert.Equal("test", result.Results[0].Name);
        Assert.Equal(BlockType.Script, result.Results[0].Identifier);
        Assert.Contains("Hello world", result.Results[0].RawContent);
    }

    [Fact]
    public void TextWithBlock_ExtractsRawContent()
    {
        var input = "text intro[Welcome]";
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Single(result.Results);
        Assert.Equal(BlockType.Text, result.Results[0].Identifier);
        Assert.Equal("intro", result.Results[0].Name);
    }

    [Fact]
    public void MultipleStatements_ExtractsAll()
    {
        var input = "script a[Hello] text b[World]";
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Equal(2, result.Results.Count);
    }

    [Fact]
    public void BlockContentPreservesNewlines()
    {
        const string input = "script test[\nline 1\nline 2\n]";
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Contains("line 1", result.Results[0].RawContent);
        Assert.Contains("line 2", result.Results[0].RawContent);
    }

    [Fact]
    public void MalformedInput_ReportsErrors()
    {
        const string input = "script test]Hello]";
        var (_, errors) = PlayscriptStructureHelper.ParseStructureWithErrors(input);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ScriptBlock_HasBlockTypeScript()
    {
        var result = PlayscriptStructureHelper.ParseStructure("script test[Hello]");
        Assert.Single(result.Results);
        Assert.Equal(BlockType.Script, result.Results[0].Identifier);
    }

    [Fact]
    public void TextBlock_HasBlockTypeText()
    {
        var result = PlayscriptStructureHelper.ParseStructure("text intro[Welcome]");
        Assert.Single(result.Results);
        Assert.Equal(BlockType.Text, result.Results[0].Identifier);
    }

    [Fact]
    public void InterfaceKeyword_ProducesParseError()
    {
        var (_, errors) = PlayscriptStructureHelper.ParseStructureWithErrors("interface foo[Hello]");
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void InterfaceDeclaration_TokenizesCorrectly()
    {
        var (_, errors) = PlayscriptStructureHelper.ParseStructureWithErrors(
            "interface transition(type: string, duration: decimal) : void");
        Assert.Empty(errors);
    }

    [Fact]
    public void InterfaceDeclaration_NoParameters_ParsesWithoutErrors()
    {
        var (_, errors) = PlayscriptStructureHelper.ParseStructureWithErrors(
            "interface on_complete() : void");
        Assert.Empty(errors);
    }

    [Fact]
    public void InterfaceDeclaration_SingleParameter_Parses()
    {
        var (_, errors) = PlayscriptStructureHelper.ParseStructureWithErrors(
            "interface transition(type: string) : void");
        Assert.Empty(errors);
    }

    [Fact]
    public void InterfaceDeclaration_MultipleParameters_Parses()
    {
        var (_, errors) = PlayscriptStructureHelper.ParseStructureWithErrors(
            "interface transition(type: string, duration: decimal) : void");
        Assert.Empty(errors);
    }

    [Fact]
    public void InterfaceDeclaration_AllParamTypes_Parses()
    {
        var (_, errors) = PlayscriptStructureHelper.ParseStructureWithErrors(
            "interface complex(a: string, b: int, c: decimal, d: bool) : void");
        Assert.Empty(errors);
    }

    [Fact]
    public void InterfaceDeclaration_AllReturnTypes_Parses()
    {
        foreach (var ret in new[] { "void", "string", "int", "decimal", "bool" })
        {
            var (_, errors) = PlayscriptStructureHelper.ParseStructureWithErrors(
                $"interface f() : {ret}");
            Assert.Empty(errors);
        }
    }

    [Fact]
    public void InterfaceDeclaration_MultiLine_Parses()
    {
        var input = "interface transition(\n  type: string,\n  duration: decimal\n) : void";
        var (_, errors) = PlayscriptStructureHelper.ParseStructureWithErrors(input);
        Assert.Empty(errors);
    }

    [Fact]
    public void UnknownKeyword_ProducesParseError()
    {
        var (_, errors) = PlayscriptStructureHelper.ParseStructureWithErrors("foo bar[Hello]");
        Assert.NotEmpty(errors);
    }

    // ─── Step 3: Interface Declaration Extraction ─────────────────────────────

    [Fact]
    public void InterfaceDeclaration_ExtractsName()
    {
        var result = PlayscriptStructureHelper.ParseStructure(
            "interface transition(type: string) : void");
        Assert.Single(result.Interfaces);
        Assert.Equal("transition", result.Interfaces[0].Name);
    }

    [Fact]
    public void InterfaceDeclaration_ExtractsParameters()
    {
        var result = PlayscriptStructureHelper.ParseStructure(
            "interface transition(type: string, duration: decimal) : void");
        Assert.Equal(2, result.Interfaces[0].Parameters.Count);
        Assert.Equal("type", result.Interfaces[0].Parameters[0].Name);
        Assert.Equal(InterfaceType.String, result.Interfaces[0].Parameters[0].Type);
        Assert.Equal("duration", result.Interfaces[0].Parameters[1].Name);
        Assert.Equal(InterfaceType.Decimal, result.Interfaces[0].Parameters[1].Type);
    }

    [Fact]
    public void InterfaceDeclaration_ExtractsReturnType()
    {
        var result = PlayscriptStructureHelper.ParseStructure(
            "interface transition(type: string) : void");
        Assert.Equal(InterfaceType.Void, result.Interfaces[0].ReturnType);
    }

    [Fact]
    public void InterfaceDeclaration_NonVoidReturnType()
    {
        var result = PlayscriptStructureHelper.ParseStructure(
            "interface get_name() : string");
        Assert.Equal(InterfaceType.String, result.Interfaces[0].ReturnType);
    }

    [Fact]
    public void InterfaceDeclaration_NoParameters_EmptyList()
    {
        var result = PlayscriptStructureHelper.ParseStructure(
            "interface on_complete() : void");
        Assert.Empty(result.Interfaces[0].Parameters);
    }

    [Fact]
    public void InterfaceDeclaration_MixedWithScriptBlocks()
    {
        const string input = """
                             interface transition(type: string) : void
                             script foo[
                             Hello @transition("fade_out")
                             ]
                             """;
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Single(result.Interfaces);
        Assert.Single(result.Results);
    }

    // ─── End-to-End Tests ─────────────────────────────────────────────────────

    private static ScriptBlock ParseEndToEnd(string input)
    {
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Single(result.Results);
        Assert.NotNull(result.Results[0].RawContent);

        var trimmed = result.Results[0].RawContent!.Trim('\r', '\n');
        var (parser, errors) = PlayscriptContentHelper.ParseScript(trimmed);
        Assert.Empty(errors);

        var builder = new PlayscriptCodeBuilder();
        builder.BuildScriptFromContent(parser.scriptContent());
        return builder.ContentResult;
    }

    [Fact]
    public void EndToEnd_NoNewlines()
    {
        var block = ParseEndToEnd("script t[Hello]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items);
        Assert.Equal("Hello", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_LeadingNewline()
    {
        var block = ParseEndToEnd("script t[\nHello]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Equal("Hello", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_LeadingTwoNewlines()
    {
        var block = ParseEndToEnd("script t[\n\nHello]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Equal("Hello", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_TrailingNewline()
    {
        var block = ParseEndToEnd("script t[Hello\n]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Equal("Hello", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_TrailingTwoNewlines()
    {
        var block = ParseEndToEnd("script t[Hello\n\n]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Equal("Hello", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_BothSidesNewlines()
    {
        var block = ParseEndToEnd("script t[\nHello\n]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Equal("Hello", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_OnlyNewlines()
    {
        var block = ParseEndToEnd("script t[\n\n\n]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Single(block.Pages[0].Paragraphs[0].Lines);
        Assert.Empty(block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items);
    }

    [Fact]
    public void EndToEnd_TwoLines()
    {
        var block = ParseEndToEnd("script t[line 1\nline 2]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Equal(2, block.Pages[0].Paragraphs[0].Lines.Count);
        Assert.Equal("line 1", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
        Assert.Equal("line 2", ((TextItem)block.Pages[0].Paragraphs[0].Lines[1].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_TwoParagraphs()
    {
        var block = ParseEndToEnd("script t[para 1\n\npara 2]");
        Assert.Single(block.Pages);
        Assert.Equal(2, block.Pages[0].Paragraphs.Count);
        Assert.Equal("para 1", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
        Assert.Equal("para 2", ((TextItem)block.Pages[0].Paragraphs[1].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_PageBreak()
    {
        var block = ParseEndToEnd("script t[p1\n/\np2]");
        Assert.Equal(2, block.Pages.Count);
        Assert.Equal("p1", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
        Assert.Equal("p2", ((TextItem)block.Pages[1].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_LeadingNewlineThenStructure()
    {
        var block = ParseEndToEnd("script t[\nline 1\nline 2]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Equal(2, block.Pages[0].Paragraphs[0].Lines.Count);
        Assert.Equal("line 1", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
        Assert.Equal("line 2", ((TextItem)block.Pages[0].Paragraphs[0].Lines[1].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_TrailingNewlineAfterStructure()
    {
        var block = ParseEndToEnd("script t[line 1\nline 2\n]");
        Assert.Single(block.Pages);
        Assert.Single(block.Pages[0].Paragraphs);
        Assert.Equal(2, block.Pages[0].Paragraphs[0].Lines.Count);
        Assert.Equal("line 1", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
        Assert.Equal("line 2", ((TextItem)block.Pages[0].Paragraphs[0].Lines[1].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_LeadingTrailingWithPageBreak()
    {
        var block = ParseEndToEnd("script t[\np1\n/\np2\n]");
        Assert.Equal(2, block.Pages.Count);
        Assert.Equal("p1", ((TextItem)block.Pages[0].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
        Assert.Equal("p2", ((TextItem)block.Pages[1].Paragraphs[0].Lines[0].Segments[0].Items[0]).Text);
    }

    [Fact]
    public void EndToEnd_TextBlock()
    {
        var result = PlayscriptStructureHelper.ParseStructure("text intro[Welcome]");
        Assert.Single(result.Results);
        Assert.NotNull(result.Results[0].RawContent);

        var trimmed = result.Results[0].RawContent!.Trim('\r', '\n');
        var (parser, errors) = PlayscriptContentHelper.ParseText(trimmed);
        Assert.Empty(errors);

        var builder = new PlayscriptCodeBuilder();
        builder.BuildTextFromContent(parser.textContent());
        var block = builder.TextResult;

        Assert.Single(block.Lines);
        Assert.Single(block.Lines[0].Segments[0].Items);
        Assert.Equal("Welcome", ((TextItem)block.Lines[0].Segments[0].Items[0]).Text);
    }

    // ─── Async Interface Parsing ─────────────────────────────────────────────

    [Fact]
    public void ParseStructure_AsyncInterface_SetsIsAsyncTrue()
    {
        var result = PlayscriptStructureHelper.ParseStructure("async interface play(sound: string) : void");
        Assert.Single(result.Interfaces);
        Assert.True(result.Interfaces[0].IsAsync);
        Assert.Equal("play", result.Interfaces[0].Name);
    }

    [Fact]
    public void ParseStructure_SyncInterface_SetsIsAsyncFalse()
    {
        var result = PlayscriptStructureHelper.ParseStructure("interface play(sound: string) : void");
        Assert.Single(result.Interfaces);
        Assert.False(result.Interfaces[0].IsAsync);
    }

    [Fact]
    public void ParseStructure_AsyncInterface_NoParameters()
    {
        var result = PlayscriptStructureHelper.ParseStructure("async interface on_complete() : void");
        Assert.Single(result.Interfaces);
        Assert.True(result.Interfaces[0].IsAsync);
        Assert.Empty(result.Interfaces[0].Parameters);
    }

    [Fact]
    public void ParseStructure_AsyncInterface_NonVoidReturn()
    {
        var result = PlayscriptStructureHelper.ParseStructure("async interface get_data(id: int) : string");
        Assert.Single(result.Interfaces);
        Assert.True(result.Interfaces[0].IsAsync);
        Assert.Equal(InterfaceType.String, result.Interfaces[0].ReturnType);
        Assert.Single(result.Interfaces[0].Parameters);
        Assert.Equal(InterfaceType.Int, result.Interfaces[0].Parameters[0].Type);
    }

    [Fact]
    public void ParseStructure_MixedSyncAndAsync()
    {
        var input = "interface play(s: string) : void\nasync interface load(id: int) : string";
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Equal(2, result.Interfaces.Count);
        Assert.False(result.Interfaces[0].IsAsync);
        Assert.True(result.Interfaces[1].IsAsync);
    }

    // ── Variation Parsing ────────────────────────────────────────────────────

    [Fact]
    public void ParseStructure_VariationBlock_ExtractsNameAndIsDefaultFalse()
    {
        var result = PlayscriptStructureHelper.ParseStructure("script foo variation casual[\nHello\n]");
        Assert.Single(result.Results);
        Assert.Equal("foo", result.Results[0].Name);
        Assert.Equal("casual", result.Results[0].Variation);
        Assert.False(result.Results[0].IsDefault);
    }

    [Fact]
    public void ParseStructure_DefaultVariationBlock_ExtractsNameAndIsDefaultTrue()
    {
        var result = PlayscriptStructureHelper.ParseStructure("script foo default variation casual[\nHello\n]");
        Assert.Single(result.Results);
        Assert.Equal("foo", result.Results[0].Name);
        Assert.Equal("casual", result.Results[0].Variation);
        Assert.True(result.Results[0].IsDefault);
    }

    [Fact]
    public void ParseStructure_DefaultVariationTextBlock()
    {
        var result = PlayscriptStructureHelper.ParseStructure("text intro default variation quick[\nWelcome\n]");
        Assert.Single(result.Results);
        Assert.Equal("intro", result.Results[0].Name);
        Assert.Equal("quick", result.Results[0].Variation);
        Assert.True(result.Results[0].IsDefault);
        Assert.Equal(BlockType.Text, result.Results[0].Identifier);
    }

    [Fact]
    public void ParseStructure_DefaultKeywordWithoutVariation_StillParsesAsUnversioned()
    {
        // DEFAULT without VARIATION is not matched by the optional group, so it's treated as unversioned
        // (the optional group is (DEFAULT? VARIATION IDENTIFIER)? so DEFAULT alone doesn't trigger the group)
        var result = PlayscriptStructureHelper.ParseStructure("script foo[\nHello\n]");
        Assert.Single(result.Results);
        Assert.Null(result.Results[0].Variation);
        Assert.False(result.Results[0].IsDefault);
    }

    // ─── Namespace Declaration Parsing ──────────────────────────────────────

    [Fact]
    public void ParseStructure_Namespace_SingleSegment()
    {
        var result = PlayscriptStructureHelper.ParseStructure("namespace Combat\nscript attack[Hi]");
        Assert.Equal("Combat", result.Namespace);
    }

    [Fact]
    public void ParseStructure_Namespace_MultiSegment()
    {
        var result = PlayscriptStructureHelper.ParseStructure(
            "namespace Alice.Bob.Charlie\nscript foo[Hi]");
        Assert.Equal("Alice.Bob.Charlie", result.Namespace);
    }

    [Fact]
    public void ParseStructure_NoNamespace_Null()
    {
        var result = PlayscriptStructureHelper.ParseStructure("script foo[Hi]");
        Assert.Null(result.Namespace);
    }

    [Fact]
    public void ParseStructure_Namespace_SetsOnInterfaces()
    {
        var result = PlayscriptStructureHelper.ParseStructure(
            "namespace Foo\ninterface greet(s: string) : void");
        Assert.Equal("Foo", result.Namespace);
        Assert.Single(result.Interfaces);
        Assert.Equal("Foo", result.Interfaces[0].Namespace);
    }

    [Fact]
    public void ParseStructure_Namespace_WithInterfaceOnly_NoErrors()
    {
        var (_, errors) = PlayscriptStructureHelper.ParseStructureWithErrors(
            "namespace UI\ninterface play(s: string) : void");
        Assert.Empty(errors);
    }

    [Fact]
    public void ParseStructure_Namespace_WithBlockOnly_NoErrors()
    {
        var (_, errors) = PlayscriptStructureHelper.ParseStructureWithErrors(
            "namespace UI\nscript menu[Hi]");
        Assert.Empty(errors);
    }

    [Fact]
    public void ParseStructure_Namespace_MixedBlocksAndInterfaces()
    {
        var result = PlayscriptStructureHelper.ParseStructure(
            "namespace Foo\ninterface a() : void\nscript b[Hi]\ntext c[Hello]");
        Assert.Equal("Foo", result.Namespace);
        Assert.Single(result.Interfaces);
        Assert.Equal("Foo", result.Interfaces[0].Namespace);
        Assert.Equal(2, result.Results.Count);
    }

    [Fact]
    public void ParseStructure_Namespace_WithAsyncInterface()
    {
        var result = PlayscriptStructureHelper.ParseStructure(
            "namespace API\nasync interface fetch(id: int) : string");
        Assert.Equal("API", result.Namespace);
        Assert.Single(result.Interfaces);
        Assert.Equal("API", result.Interfaces[0].Namespace);
        Assert.True(result.Interfaces[0].IsAsync);
    }

    [Fact]
    public void ParseStructure_Namespace_BeforeVariation()
    {
        var result = PlayscriptStructureHelper.ParseStructure(
            "namespace Foo\nscript bar variation casual[Hi]");
        Assert.Equal("Foo", result.Namespace);
        Assert.Single(result.Results);
        Assert.Equal("bar", result.Results[0].Name);
        Assert.Equal("casual", result.Results[0].Variation);
    }

    [Fact]
    public void ParseStructure_Namespace_BeforeDefaultVariation()
    {
        var result = PlayscriptStructureHelper.ParseStructure(
            "namespace Foo\ntext intro default variation quick[Welcome]");
        Assert.Equal("Foo", result.Namespace);
        Assert.Single(result.Results);
        Assert.Equal("intro", result.Results[0].Name);
        Assert.Equal("quick", result.Results[0].Variation);
        Assert.True(result.Results[0].IsDefault);
    }
}