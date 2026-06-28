using EasyPlayscript.LSP.Parsing;
using EasyPlayscript.LSP.Semantic;
using Xunit.Abstractions;

namespace EasyPlayscript.LSP.Tests;

public class DiagnosticTokenTests
{
    private readonly ITestOutputHelper _output;

    public DiagnosticTokenTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const string ValidInput = """
        async interface fetch_user_name(user_id: int) : string
        async interface log_event(event: string) : void

        script async_demo[
        正在加载用户信息……

        @log_event("demo_started")

        用户：@fetch_user_name(42)，你好！

        @log_event("demo_finished")
        ]
        """;

    private const string InvalidInput = """
        async interface fetch_user_name(user_id: int) : string
        sync interface log_event(event: string) : void

        script async_demo[
        正在加载用户信息……

        @log_event("demo_started")

        用户：@fetch_user_name(42)，你好！

        @log_event("demo_finished")
        ]
        """;

    private static string TypeName(int t) => t switch
    {
        SemanticTokenTypes.Keyword => "keyword",
        SemanticTokenTypes.Modifier => "modifier",
        SemanticTokenTypes.Type => "type",
        SemanticTokenTypes.Function => "function",
        SemanticTokenTypes.String => "string",
        SemanticTokenTypes.Number => "number",
        SemanticTokenTypes.Comment => "comment",
        SemanticTokenTypes.Operator => "operator",
        SemanticTokenTypes.Variable => "variable",
        SemanticTokenTypes.Boolean => "boolean",
        _ => $"?{t}"
    };

    private static void DumpTokens(ITestOutputHelper output, string label, IReadOnlyList<TokenEntry> tokens)
    {
        output.WriteLine($"=== {label} ({tokens.Count} tokens) ===");
        output.WriteLine($"{"Line",4} {"Col",4} {"Len",4} {"Type",-10} {"Mods",4}");
        output.WriteLine(new string('-', 32));
        foreach (var t in tokens)
            output.WriteLine($"{t.Line,4} {t.Col,4} {t.Length,4} {TypeName(t.TokenType),-10} {t.TokenModifiers,4}");
    }

    [Fact]
    public void Dump_ValidInput()
    {
        var doc = PlayscriptDocumentParser.Parse(ValidInput);
        DumpTokens(_output, "Valid (async)", doc.Tokens);
    }

    [Fact]
    public void Dump_InvalidInput()
    {
        var doc = PlayscriptDocumentParser.Parse(InvalidInput);
        DumpTokens(_output, "Invalid (sync)", doc.Tokens);
    }

    [Fact]
    public void ContentTokens_AreIdenticalBetweenValidAndInvalid()
    {
        var valid = PlayscriptDocumentParser.Parse(ValidInput);
        var invalid = PlayscriptDocumentParser.Parse(InvalidInput);

        var validContent = valid.Tokens
            .Where(t => t.TokenType == SemanticTokenTypes.Function ||
                        t.TokenType == SemanticTokenTypes.String ||
                        t.TokenType == SemanticTokenTypes.Number ||
                        t.TokenType == SemanticTokenTypes.Boolean)
            .ToArray();

        var invalidContent = invalid.Tokens
            .Where(t => t.TokenType == SemanticTokenTypes.Function ||
                        t.TokenType == SemanticTokenTypes.String ||
                        t.TokenType == SemanticTokenTypes.Number ||
                        t.TokenType == SemanticTokenTypes.Boolean)
            .ToArray();

        _output.WriteLine($"Valid content tokens: {validContent.Length}");
        _output.WriteLine($"Invalid content tokens: {invalidContent.Length}");

        DumpTokens(_output, "Valid content tokens", validContent);
        DumpTokens(_output, "Invalid content tokens", invalidContent);

        Assert.Equal(validContent.Length, invalidContent.Length);

        for (var i = 0; i < validContent.Length; i++)
        {
            var v = validContent[i];
            var iv = invalidContent[i];
            Assert.True(v.Line == iv.Line && v.Col == iv.Col && v.Length == iv.Length &&
                        v.TokenType == iv.TokenType && v.TokenModifiers == iv.TokenModifiers,
                $"Content token {i} differs:\n  valid:   ({v.Line},{v.Col},{v.Length},{TypeName(v.TokenType)})\n  invalid: ({iv.Line},{iv.Col},{iv.Length},{TypeName(iv.TokenType)})");
        }
    }

    [Fact]
    public void StructureTokens_ShiftOnlyOnChangedLine()
    {
        var valid = PlayscriptDocumentParser.Parse(ValidInput);
        var invalid = PlayscriptDocumentParser.Parse(InvalidInput);

        // Structure tokens: keyword, modifier, type, variable, operator
        var validStructure = valid.Tokens
            .Where(t => t.TokenType == SemanticTokenTypes.Keyword ||
                        t.TokenType == SemanticTokenTypes.Modifier ||
                        t.TokenType == SemanticTokenTypes.Type ||
                        t.TokenType == SemanticTokenTypes.Variable ||
                        t.TokenType == SemanticTokenTypes.Operator)
            .ToArray();

        var invalidStructure = invalid.Tokens
            .Where(t => t.TokenType == SemanticTokenTypes.Keyword ||
                        t.TokenType == SemanticTokenTypes.Modifier ||
                        t.TokenType == SemanticTokenTypes.Type ||
                        t.TokenType == SemanticTokenTypes.Variable ||
                        t.TokenType == SemanticTokenTypes.Operator)
            .ToArray();

        DumpTokens(_output, "Valid structure tokens", validStructure);
        DumpTokens(_output, "Invalid structure tokens", invalidStructure);

        // Tokens on line 0 (first declaration) should be identical
        var validLine0 = validStructure.Where(t => t.Line == 0).ToArray();
        var invalidLine0 = invalidStructure.Where(t => t.Line == 0).ToArray();

        _output.WriteLine($"\nLine 0: valid={validLine0.Length}, invalid={invalidLine0.Length}");
        Assert.Equal(validLine0.Length, invalidLine0.Length);

        for (var i = 0; i < validLine0.Length; i++)
        {
            Assert.True(validLine0[i].Col == invalidLine0[i].Col &&
                        validLine0[i].Length == invalidLine0[i].Length &&
                        validLine0[i].TokenType == invalidLine0[i].TokenType,
                $"Line 0 token {i} differs");
        }

        // Content block tokens (line >= 3) should be identical
        var validBlock = validStructure.Where(t => t.Line >= 3).ToArray();
        var invalidBlock = invalidStructure.Where(t => t.Line >= 3).ToArray();

        _output.WriteLine($"\nContent block: valid={validBlock.Length}, invalid={invalidBlock.Length}");
        DumpTokens(_output, "Valid block tokens (line>=3)", validBlock);
        DumpTokens(_output, "Invalid block tokens (line>=3)", invalidBlock);

        Assert.Equal(validBlock.Length, invalidBlock.Length);

        for (var i = 0; i < validBlock.Length; i++)
        {
            Assert.True(validBlock[i].Line == invalidBlock[i].Line &&
                        validBlock[i].Col == invalidBlock[i].Col &&
                        validBlock[i].Length == invalidBlock[i].Length &&
                        validBlock[i].TokenType == invalidBlock[i].TokenType,
                $"Block token {i} differs:\n  valid:   ({validBlock[i].Line},{validBlock[i].Col},{validBlock[i].Length},{TypeName(validBlock[i].TokenType)})\n  invalid: ({invalidBlock[i].Line},{invalidBlock[i].Col},{invalidBlock[i].Length},{TypeName(invalidBlock[i].TokenType)})");
        }
    }

    [Fact]
    public void AllTokens_SortedByPosition()
    {
        var doc = PlayscriptDocumentParser.Parse(InvalidInput);
        for (var i = 1; i < doc.Tokens.Count; i++)
        {
            var prev = doc.Tokens[i - 1];
            var curr = doc.Tokens[i];
            Assert.True(curr.Line > prev.Line || (curr.Line == prev.Line && curr.Col >= prev.Col),
                $"Tokens not sorted: [{i - 1}] ({prev.Line},{prev.Col}) > [{i}] ({curr.Line},{curr.Col})");
        }
    }
}
