using EasyPlayscript.LSP.Parsing;
using EasyPlayscript.LSP.Semantic;

namespace EasyPlayscript.LSP.Tests;

public class PlayscriptDocumentParserTests
{
    private readonly PlayscriptDocumentParser _parser = new();

    // ── Structure tokens ─────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyFile_ReturnsNoTokens()
    {
        var doc = PlayscriptDocumentParser.Parse("");
        Assert.Empty(doc.Tokens);
    }

    [Fact]
    public void Parse_ScriptBlock_EmitsKeywordAndVariableTokens()
    {
        var doc = PlayscriptDocumentParser.Parse("script hello[world]");
        var types = doc.Tokens.Select(t => t.TokenType).ToArray();

        Assert.Contains(SemanticTokenTypes.Keyword, types);   // script
        Assert.Contains(SemanticTokenTypes.Variable, types);  // hello
    }

    [Fact]
    public void Parse_TextBlock_EmitsKeywordAndVariableTokens()
    {
        var doc = PlayscriptDocumentParser.Parse("text greeting[hello]");
        var types = doc.Tokens.Select(t => t.TokenType).ToArray();

        Assert.Contains(SemanticTokenTypes.Keyword, types);   // text
        Assert.Contains(SemanticTokenTypes.Variable, types);  // greeting
    }

    [Fact]
    public void Parse_InterfaceDeclaration_EmitsKeywordsAndTypes()
    {
        var doc = PlayscriptDocumentParser.Parse("interface play(sound: string) : void");
        var types = doc.Tokens.Select(t => t.TokenType).ToArray();

        Assert.Contains(SemanticTokenTypes.Keyword, types);   // interface
        Assert.Contains(SemanticTokenTypes.Type, types);      // string, void
        Assert.Contains(SemanticTokenTypes.Variable, types);  // play, sound
        Assert.Contains(SemanticTokenTypes.Operator, types);  // :, (, )
    }

    [Fact]
    public void Parse_AsyncInterface_EmitsModifier()
    {
        var doc = PlayscriptDocumentParser.Parse("async interface fetch(id: int) : string");
        var types = doc.Tokens.Select(t => t.TokenType).ToArray();

        Assert.Contains(SemanticTokenTypes.Modifier, types);  // async
    }

    [Fact]
    public void Parse_TypeKeywords_AllFiveTypes_EmitTypeToken()
    {
        var doc = PlayscriptDocumentParser.Parse("interface test(a: string, b: int, c: decimal, d: bool) : void");
        var typeCount = doc.Tokens.Count(t => t.TokenType == SemanticTokenTypes.Type);

        Assert.Equal(5, typeCount); // string, int, decimal, bool, void
    }

    [Fact]
    public void Parse_MultipleBlocks_EmitsStructureKeywords()
    {
        var doc = PlayscriptDocumentParser.Parse("script a[hello] text b[world]");
        var keywordCount = doc.Tokens.Count(t => t.TokenType == SemanticTokenTypes.Keyword);

        Assert.Equal(2, keywordCount); // script, text
    }

    // ── Content tokens ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_ScriptWithText_EmitsStringToken()
    {
        var doc = PlayscriptDocumentParser.Parse("script test[hello world]");
        Assert.Contains(doc.Tokens, t => t.TokenType == SemanticTokenTypes.String);
    }

    [Fact]
    public void Parse_ConsumerCall_EmitsOperatorAndFunctionTokens()
    {
        var doc = PlayscriptDocumentParser.Parse("script test[@on_complete()]");
        var types = doc.Tokens.Select(t => t.TokenType).ToArray();

        Assert.Contains(SemanticTokenTypes.Function, types);  // on_complete
        Assert.Contains(SemanticTokenTypes.Operator, types);  // @, (, )
    }

    [Fact]
    public void Parse_ConsumerCallWithStringArg_EmitsStringToken()
    {
        var doc = PlayscriptDocumentParser.Parse("script test[@transition(\"fade\")]");
        var stringTokens = doc.Tokens.Where(t => t.TokenType == SemanticTokenTypes.String).ToArray();

        Assert.Contains(stringTokens, t => t.Length > 1); // at least the string literal
    }

    [Fact]
    public void Parse_ConsumerCallWithIntArg_EmitsNumberToken()
    {
        var doc = PlayscriptDocumentParser.Parse("script test[@fetch(42)]");
        Assert.Contains(doc.Tokens, t => t.TokenType == SemanticTokenTypes.Number);
    }

    [Fact]
    public void Parse_ConsumerCallWithBoolArg_EmitsBooleanToken()
    {
        var doc = PlayscriptDocumentParser.Parse("script test[@config(true)]");
        var booleanTokens = doc.Tokens.Where(t => t.TokenType == SemanticTokenTypes.Boolean).ToArray();
        Assert.NotEmpty(booleanTokens);
    }

    [Fact]
    public void Parse_FloatArg_EmitsNumberToken()
    {
        var doc = PlayscriptDocumentParser.Parse("script test[@play(0.8)]");
        var numberTokens = doc.Tokens.Where(t => t.TokenType == SemanticTokenTypes.Number).ToArray();
        Assert.NotEmpty(numberTokens);
    }

    [Fact]
    public void Parse_InlineCallInText_EmitsMixedTokens()
    {
        var doc = PlayscriptDocumentParser.Parse("script test[hello @get_name() world]");
        var types = doc.Tokens.Select(t => t.TokenType).Distinct().ToArray();

        Assert.Contains(SemanticTokenTypes.String, types);    // text parts
        Assert.Contains(SemanticTokenTypes.Function, types);  // get_name
        Assert.Contains(SemanticTokenTypes.Operator, types);  // @, (, )
    }

    [Fact]
    public void Parse_CommentInContent_EmitsCommentToken()
    {
        var doc = PlayscriptDocumentParser.Parse("script test[\n# a comment\nhello]");
        Assert.Contains(doc.Tokens, t => t.TokenType == SemanticTokenTypes.Comment);
    }

    [Fact]
    public void Parse_PageBreakSlash_EmitsOperatorToken()
    {
        var doc = PlayscriptDocumentParser.Parse("script test[page one\n/\npage two]");
        var slashTokens = doc.Tokens.Where(t =>
            t.TokenType == SemanticTokenTypes.Operator && t.Length == 1).ToArray();

        Assert.NotEmpty(slashTokens);
    }

    [Fact]
    public void Parse_EmptyBlock_ReturnsNoContentTokens()
    {
        var doc = PlayscriptDocumentParser.Parse("script test[]");
        // Only structure tokens: script, test, [, ]
        Assert.All(doc.Tokens, t =>
            Assert.True(t.TokenType == SemanticTokenTypes.Keyword ||
                        t.TokenType == SemanticTokenTypes.Variable ||
                        t.TokenType == SemanticTokenTypes.Operator));
    }

    [Fact]
    public void Parse_TextBlock_EmitsContentTokens()
    {
        var doc = PlayscriptDocumentParser.Parse("text welcome[Hello @get_name()]");
        Assert.Contains(doc.Tokens, t => t.TokenType == SemanticTokenTypes.Function);
        Assert.Contains(doc.Tokens, t => t.TokenType == SemanticTokenTypes.String);
    }

    // ── Position mapping ─────────────────────────────────────────────────────

    [Fact]
    public void Parse_ContentTokens_HaveAbsolutePositions()
    {
        var doc = PlayscriptDocumentParser.Parse("script test[\nhello]");
        var textToken = doc.Tokens.FirstOrDefault(t => t.TokenType == SemanticTokenTypes.String);

        // "hello" is on line 2 of the file (1-based in ANTLR → 1 in LSP 0-based)
        Assert.True(textToken.Line >= 0, $"Expected line >= 0, got {textToken.Line}");
    }

    [Fact]
    public void Parse_MultilineContent_LinesMapCorrectly()
    {
        var input = "script test[\nline one\nline two\n]";
        var doc = PlayscriptDocumentParser.Parse(input);
        var textTokens = doc.Tokens.Where(t => t.TokenType == SemanticTokenTypes.String).ToArray();

        // Two text tokens on different lines
        Assert.True(textTokens.Length >= 2);
        Assert.NotEqual(textTokens[0].Line, textTokens[1].Line);
    }

    [Fact]
    public void Parse_SecondBlock_OffsetFromFirst()
    {
        var input = "script a[first] script b[second]";
        var doc = PlayscriptDocumentParser.Parse(input);
        var textTokens = doc.Tokens.Where(t => t.TokenType == SemanticTokenTypes.String).ToArray();

        Assert.Equal(2, textTokens.Length);
        // "second" should be at a higher column than "first"
        Assert.True(textTokens[1].Col > textTokens[0].Col || textTokens[1].Line > textTokens[0].Line);
    }

    // ── Errors ───────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_LexerError_InStructure_ReturnsError()
    {
        var doc = PlayscriptDocumentParser.Parse("script test]bad]");
        Assert.NotEmpty(doc.Errors);
    }

    [Fact]
    public void Parse_ValidFile_ReturnsNoErrors()
    {
        var doc = PlayscriptDocumentParser.Parse("script test[hello world]");
        Assert.Empty(doc.Errors);
    }

    // ── Block offsets ────────────────────────────────────────────────────────

    [Fact]
    public void ComputeBlockOffsets_SingleBlock()
    {
        var offsets = PlayscriptDocumentParser.ComputeBlockOffsets("script a[hello]");
        Assert.Single(offsets);
    }

    [Fact]
    public void ComputeBlockOffsets_TwoBlocks()
    {
        var offsets = PlayscriptDocumentParser.ComputeBlockOffsets("script a[hello] script b[world]");
        Assert.Equal(2, offsets.Count);
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_NewlineOnlyContent_ReturnsNoContentTokens()
    {
        var doc = PlayscriptDocumentParser.Parse("script test[\n]");
        // Only structure tokens: script, test, [, ]
        Assert.All(doc.Tokens, t =>
            Assert.True(t.TokenType == SemanticTokenTypes.Keyword ||
                        t.TokenType == SemanticTokenTypes.Variable ||
                        t.TokenType == SemanticTokenTypes.Operator));
    }

    [Fact]
    public void Parse_InterfaceOnlyFile_ReturnsStructureTokens()
    {
        var doc = PlayscriptDocumentParser.Parse("interface foo(x: int) : string");
        Assert.NotEmpty(doc.Tokens);
        Assert.Contains(doc.Tokens, t => t.TokenType == SemanticTokenTypes.Keyword);
        Assert.Contains(doc.Tokens, t => t.TokenType == SemanticTokenTypes.Type);
        Assert.Empty(doc.Errors);
    }

    [Fact]
    public void Parse_ContentToken_HasExactPosition()
    {
        // "script test[\nhello]"
        // Line 1: "script test[" — structure tokens
        // Line 2: "hello]" — content
        var doc = PlayscriptDocumentParser.Parse("script test[\nhello]");
        var textToken = doc.Tokens.FirstOrDefault(t => t.TokenType == SemanticTokenTypes.String);

        // "hello" starts at ANTLR line 2 col 0 → LSP line 1 col 0
        Assert.Equal(1, textToken.Line);
        Assert.Equal(0, textToken.Col);
        Assert.Equal(5, textToken.Length); // "hello"
    }

    [Fact]
    public void Parse_MultilineContent_HasCorrectLineNumbers()
    {
        var input = "script test[\nline one\nline two\n]";
        var doc = PlayscriptDocumentParser.Parse(input);
        var textTokens = doc.Tokens.Where(t => t.TokenType == SemanticTokenTypes.String).ToArray();

        Assert.Equal(2, textTokens.Length);
        Assert.Equal(1, textTokens[0].Line); // line one → LSP line 1
        Assert.Equal(2, textTokens[1].Line); // line two → LSP line 2
    }

    // ── Parse result metadata ───────────────────────────────────────────────

    [Fact]
    public void Parse_ResultHasBlockCache()
    {
        var doc = PlayscriptDocumentParser.Parse("script a[hello] text b[world]");
        Assert.NotNull(doc.BlockCache);
        Assert.Equal(2, doc.BlockCache.Count);
    }

    [Fact]
    public void Parse_ResultHasText()
    {
        var input = "script a[hello]";
        var doc = PlayscriptDocumentParser.Parse(input);
        Assert.Equal(input, doc.Text);
    }

    // ── ParseIncremental ────────────────────────────────────────────────────

    [Fact]
    public void ParseIncremental_NoPrevious_EqualsFullParse()
    {
        var input = "script a[hello] text b[world]";
        var full = PlayscriptDocumentParser.Parse(input);
        var incremental = PlayscriptDocumentParser.ParseIncremental(input, null);

        Assert.Equal(full.Tokens.Count, incremental.Tokens.Count);
        Assert.Equal(full.Errors.Count, incremental.Errors.Count);

        for (var i = 0; i < full.Tokens.Count; i++)
        {
            Assert.Equal(full.Tokens[i].Line, incremental.Tokens[i].Line);
            Assert.Equal(full.Tokens[i].Col, incremental.Tokens[i].Col);
            Assert.Equal(full.Tokens[i].Length, incremental.Tokens[i].Length);
            Assert.Equal(full.Tokens[i].TokenType, incremental.Tokens[i].TokenType);
        }
    }

    [Fact]
    public void ParseIncremental_EditInsideBlock_ReparsesOnlyThatBlock()
    {
        var input1 = "script a[hello]\ntext b[world]";
        var input2 = "script a[hi]\ntext b[world]";

        var doc1 = PlayscriptDocumentParser.ParseIncremental(input1, null);
        var doc2 = PlayscriptDocumentParser.ParseIncremental(input2, doc1);

        // Block "b" is unchanged — its tokens should be identical in position
        var b1 = doc1.Tokens
            .Where(t => t.TokenType == SemanticTokenTypes.String && t.Line >= 1)
            .OrderBy(t => t.Col).ToArray();
        var b2 = doc2.Tokens
            .Where(t => t.TokenType == SemanticTokenTypes.String && t.Line >= 1)
            .OrderBy(t => t.Col).ToArray();

        Assert.Equal(b1.Length, b2.Length);
        for (var i = 0; i < b1.Length; i++)
        {
            Assert.Equal(b1[i].Line, b2[i].Line);
            Assert.Equal(b1[i].Col, b2[i].Col);
            Assert.Equal(b1[i].Length, b2[i].Length);
        }
    }

    [Fact]
    public void ParseIncremental_EditAboveBlock_AdjustsLineOffsets()
    {
        var input1 = "script a[hello]\ntext b[world]";
        var input2 = "script a[hello\nextra]\ntext b[world]";

        var doc1 = PlayscriptDocumentParser.ParseIncremental(input1, null);
        var doc2 = PlayscriptDocumentParser.ParseIncremental(input2, doc1);

        // Block "b" shifted down by 1 line (new line in block "a")
        var b1 = doc1.Tokens
            .Where(t => t.TokenType == SemanticTokenTypes.String && t.Line >= 1)
            .OrderBy(t => t.Col).ToArray();
        var b2 = doc2.Tokens
            .Where(t => t.TokenType == SemanticTokenTypes.String && t.Line >= 2)
            .OrderBy(t => t.Col).ToArray();

        Assert.Equal(b1.Length, b2.Length);
        for (var i = 0; i < b1.Length; i++)
        {
            Assert.Equal(b1[i].Line + 1, b2[i].Line);
        }
    }

    [Fact]
    public void ParseIncremental_BlockAdded_ParsesNewBlock()
    {
        var input1 = "script a[hello]";
        var input2 = "script a[hello] text b[world]";

        var doc1 = PlayscriptDocumentParser.ParseIncremental(input1, null);
        var doc2 = PlayscriptDocumentParser.ParseIncremental(input2, doc1);

        var contentTokens1 = doc1.Tokens.Where(t => t.TokenType == SemanticTokenTypes.String).ToArray();
        var contentTokens2 = doc2.Tokens.Where(t => t.TokenType == SemanticTokenTypes.String).ToArray();

        Assert.Equal(1, contentTokens1.Length); // only "hello"
        Assert.Equal(2, contentTokens2.Length); // "hello" + "world"
    }

    [Fact]
    public void ParseIncremental_BlockRemoved_DropsFromCache()
    {
        var input1 = "script a[hello] text b[world]";
        var input2 = "script a[hello]";

        var doc1 = PlayscriptDocumentParser.ParseIncremental(input1, null);
        var doc2 = PlayscriptDocumentParser.ParseIncremental(input2, doc1);

        var contentTokens1 = doc1.Tokens.Where(t => t.TokenType == SemanticTokenTypes.String).ToArray();
        var contentTokens2 = doc2.Tokens.Where(t => t.TokenType == SemanticTokenTypes.String).ToArray();

        Assert.Equal(2, contentTokens1.Length);
        Assert.Equal(1, contentTokens2.Length);
    }

    [Fact]
    public void ParseIncremental_BlockRenamed_InvalidatesCache()
    {
        var input1 = "script a[hello]";
        var input2 = "script b[hello]";

        var doc1 = PlayscriptDocumentParser.ParseIncremental(input1, null);
        var doc2 = PlayscriptDocumentParser.ParseIncremental(input2, doc1);

        // Block renamed from "a" to "b" — variable token changes
        var var1 = doc1.Tokens.Where(t => t.TokenType == SemanticTokenTypes.Variable).ToArray();
        var var2 = doc2.Tokens.Where(t => t.TokenType == SemanticTokenTypes.Variable).ToArray();

        Assert.Equal("a", GetTokenText(input1, var1[0]));
        Assert.Equal("b", GetTokenText(input2, var2[0]));
    }

    [Fact]
    public void ParseIncremental_EmptyFile_NoTokens()
    {
        var doc = PlayscriptDocumentParser.ParseIncremental("", null);
        Assert.Empty(doc.Tokens);
        Assert.Empty(doc.Errors);
    }

    [Fact]
    public void ParseIncremental_CachePopulated()
    {
        var input = "script a[hello] text b[world]";
        var doc = PlayscriptDocumentParser.ParseIncremental(input, null);

        Assert.NotNull(doc.BlockCache);
        Assert.Equal(2, doc.BlockCache.Count);
        Assert.True(doc.BlockCache.ContainsKey("a"));
        Assert.True(doc.BlockCache.ContainsKey("b"));
    }

    [Fact]
    public void ParseIncremental_ErrorInBlock_OnlyThatBlockErrors()
    {
        var input1 = "script a[hello] script b[world]";
        var doc1 = PlayscriptDocumentParser.ParseIncremental(input1, null);
        Assert.Empty(doc1.Errors);

        // Introduce a structure error that removes block "b"
        var input2 = "script a[hello] script b]bad]";
        var doc2 = PlayscriptDocumentParser.ParseIncremental(input2, doc1);
        Assert.NotEmpty(doc2.Errors);
    }

    [Fact]
    public void ParseIncremental_TextProperty_Set()
    {
        var input = "script a[hello]";
        var doc = PlayscriptDocumentParser.ParseIncremental(input, null);
        Assert.Equal(input, doc.Text);
    }

    [Fact]
    public void ParseIncremental_SequentialEdits_PreservesUnchangedBlocks()
    {
        var input1 = "script a[line1]\ntext b[line2]\nscript c[line3]";
        var input2 = "script a[line1 edited]\ntext b[line2]\nscript c[line3]";
        var input3 = "script a[line1 edited]\ntext b[line2 edited]\nscript c[line3]";

        var doc1 = PlayscriptDocumentParser.ParseIncremental(input1, null);
        var doc2 = PlayscriptDocumentParser.ParseIncremental(input2, doc1);
        var doc3 = PlayscriptDocumentParser.ParseIncremental(input3, doc2);

        // Block "c" unchanged across all 3 — tokens at same position
        var c1 = doc1.Tokens.Where(t => t.TokenType == SemanticTokenTypes.String && t.Line >= 2)
            .OrderBy(t => t.Col).ToArray();
        var c3 = doc3.Tokens.Where(t => t.TokenType == SemanticTokenTypes.String && t.Line >= 2)
            .OrderBy(t => t.Col).ToArray();

        Assert.Equal(c1.Length, c3.Length);
        for (var i = 0; i < c1.Length; i++)
        {
            Assert.Equal(c1[i].Line, c3[i].Line);
            Assert.Equal(c1[i].Col, c3[i].Col);
        }
    }

    private static string GetTokenText(string source, TokenEntry token)
    {
        // Structure tokens map to source directly via ANTLR positions (1-based line, 0-based col)
        // But our tokens use LSP positions (0-based line). For single-line inputs, line 0 maps to source line 1.
        var lines = source.Split('\n');
        var line = lines[token.Line]; // LSP 0-based maps to array index directly
        return line.Substring(token.Col, token.Length);
    }
}
