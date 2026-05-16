using System.Linq;
using EasyPlayscript.Parsing;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptParserTests
{
    private const string Example = """
        # This is a comment.

        # This is a external function call. All statements starts with a @ is considered as an external tool call
        # which calls the corresponding C# code. The string inside its paranthesis is the parameter. The content
        # inside the square bracket is a "script block".
        @script("load tooltip")[
        # "aaa \n bbb" is seen as one sentence. For example, the following structure should be parsed as "您好。这里是……？"
        你好。
        这里是……？

        # "aaa \n\n bbb" is seen as two sentences. For example, the following structure should be parsed as "啊、您好\n请问你是？"
        啊、您好！

        请问你是？

        # External function calls can also be made from inside the script block. For example:
        @transistion("fade_out")

        # But script blocks cannot be nested in another script block. For example, the following is illegal:
        # @script("something inside load tooltip")[...] <-- ILLEGAL FOR NESTED BLOCKS!
        ]
        """;

    [Fact]
    public void ExampleFile_ParsesWithoutErrors()
    {
        var input = Example;
        var (parser, errors) = PlayscriptParserHelper.Parse(input);
        parser.playscript();

        Assert.Empty(errors);
    }

    [Fact]
    public void ExampleFile_HasTwoStatements()
    {
        var (parser, errors) = PlayscriptParserHelper.Parse(Example);
        var tree = parser.playscript();

        var statements = tree.statement();
        Assert.Equal(2, statements.Length);
    }

    [Fact]
    public void ExampleFile_FirstStatement_IsExternalCall()
    {
        var (parser, _) = PlayscriptParserHelper.Parse(Example);
        var tree = parser.playscript();

        var externalCall = tree.statement(0).externalCall();
        Assert.NotNull(externalCall);
        Assert.Equal("\"load tooltip\"", externalCall.STRING_LITERAL().GetText());
    }

    [Fact]
    public void ExampleFile_SecondStatement_IsScriptBlock()
    {
        var (parser, _) = PlayscriptParserHelper.Parse(Example);
        var tree = parser.playscript();

        var scriptBlock = tree.statement(1).scriptBlock();
        Assert.NotNull(scriptBlock);
    }

    [Fact]
    public void ExampleFile_ScriptBlock_Sentence1_MultiLineConcat()
    {
        var input = Example;
        var (parser, _) = PlayscriptParserHelper.Parse(input);
        var tree = parser.playscript();

        var scriptBlock = tree.statement(1).scriptBlock();
        var contents = scriptBlock.scriptContent();

        var sentence1 = contents[2].sentence();
        Assert.NotNull(sentence1);

        var parts = sentence1.sentencePart();
        Assert.Equal(2, parts.Length);
        Assert.Equal("你好。", parts[0].GetText());
        Assert.Equal("这里是……？", parts[1].GetText());

        Assert.Single(sentence1.SINGLE_NEWLINE());
    }

    [Fact]
    public void ExampleFile_ScriptBlock_Sentence2_SingleLine()
    {
        var input = Example;
        var (parser, _) = PlayscriptParserHelper.Parse(input);
        var tree = parser.playscript();

        var scriptBlock = tree.statement(1).scriptBlock();
        var contents = scriptBlock.scriptContent();

        var sentence2 = contents[5].sentence();
        Assert.NotNull(sentence2);

        var parts = sentence2.sentencePart();
        Assert.Single(parts);
        Assert.Equal("啊、您好！", parts[0].GetText());
    }

    [Fact]
    public void ExampleFile_ScriptBlock_Sentence3_SingleLine()
    {
        var input = Example;
        var (parser, _) = PlayscriptParserHelper.Parse(input);
        var tree = parser.playscript();

        var scriptBlock = tree.statement(1).scriptBlock();
        var contents = scriptBlock.scriptContent();

        var sentence3 = contents[7].sentence();
        Assert.NotNull(sentence3);

        var parts = sentence3.sentencePart();
        Assert.Single(parts);
        Assert.Equal("请问你是？", parts[0].GetText());
    }

    [Fact]
    public void ExampleFile_ScriptBlock_HasInternalCall()
    {
        var input = Example;
        var (parser, _) = PlayscriptParserHelper.Parse(input);
        var tree = parser.playscript();

        var scriptBlock = tree.statement(1).scriptBlock();
        var contents = scriptBlock.scriptContent();

        var internalCall = contents
            .Select(c => c.internalCall())
            .FirstOrDefault(ic => ic != null);
        Assert.NotNull(internalCall);
        Assert.Equal("\"fade_out\"", internalCall.STRING_LITERAL().GetText());
    }

    [Fact]
    public void OrphanedScriptBlock_ReportsSCPT001()
    {
        var input = """
            [
            Hello world
            ]
            """;
        var (parser, _) = PlayscriptParserHelper.Parse(input);
        var tree = parser.playscript();

        var builder = new PlayscriptCodeBuilder();
        builder.Visit(tree);

        Assert.Single(builder.Errors);
        Assert.Contains("Script block must follow an external call", builder.Errors[0].msg);
    }

    [Fact]
    public void ValidPairing_NoSCPT001()
    {
        var input = """
            @script("test")[
            Hello world
            ]
            """;
        var (parser, _) = PlayscriptParserHelper.Parse(input);
        var tree = parser.playscript();

        var builder = new PlayscriptCodeBuilder();
        builder.Visit(tree);

        Assert.Empty(builder.Errors);
        Assert.Single(builder.Result.Scripts["test"]);
    }

    [Fact]
    public void StandaloneExternalCall_NoError()
    {
        var input = """
            @script("test")
            """;
        var (parser, _) = PlayscriptParserHelper.Parse(input);
        var tree = parser.playscript();

        var builder = new PlayscriptCodeBuilder();
        builder.Visit(tree);

        Assert.Empty(builder.Errors);
        Assert.Empty(builder.Result.Scripts);
    }
}
