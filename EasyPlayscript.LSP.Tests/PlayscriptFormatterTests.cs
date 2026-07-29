using EasyPlayscript.LSP.Services;

namespace EasyPlayscript.LSP.Tests;

public class PlayscriptFormatterTests
{
    private readonly PlayscriptFormatter _formatter = new();

    [Fact]
    public void Format_SingleScript_BracketOnSameLine()
    {
        var input = "script Greet [Hello!]";
        var result = _formatter.Format(input);

        Assert.Equal("script Greet\n[\n\tHello!\n]", result);
    }

    [Fact]
    public void Format_SingleText_BracketOnSameLine()
    {
        var input = "text Lore [Once upon a time...]";
        var result = _formatter.Format(input);

        Assert.Equal("text Lore\n[\n\tOnce upon a time...\n]", result);
    }

    [Fact]
    public void Format_MultiLineContent()
    {
        var input = "script Poem [Roses are red\nViolets are blue]";
        var result = _formatter.Format(input);

        Assert.Equal("script Poem\n[\n\tRoses are red\n\tViolets are blue\n]", result);
    }

    [Fact]
    public void Format_EmptyContent()
    {
        var input = "script Empty []";
        var result = _formatter.Format(input);

        Assert.Equal("script Empty\n[\n]", result);
    }

    [Fact]
    public void Format_WhitespaceOnlyContent()
    {
        var input = "script Whisper [   \n\n  ]";
        var result = _formatter.Format(input);

        Assert.Equal("script Whisper\n[\n]", result);
    }

    [Fact]
    public void Format_TrailingWhitespaceOnContentLines()
    {
        var input = "script Clean [hello   \nworld\t]";
        var result = _formatter.Format(input);

        Assert.Equal("script Clean\n[\n\thello\n\tworld\n]", result);
    }

    [Fact]
    public void Format_PreservesIndentation()
    {
        var input = "    script Indented [content]";
        var result = _formatter.Format(input);

        Assert.Equal("    script Indented\n    [\n    \tcontent\n    ]", result);
    }

    [Fact]
    public void Format_DeepIndentation()
    {
        var input = "\t\tscript Deep [line]";
        var result = _formatter.Format(input);

        Assert.Equal("\t\tscript Deep\n\t\t[\n\t\t\tline\n\t\t]", result);
    }

    [Fact]
    public void Format_MultipleBlocks_WithContentBetween()
    {
        var input = "script First [A]\n\n# comment\n\nscript Second [B]";
        var result = _formatter.Format(input);

        Assert.Equal("script First\n[\n\tA\n]\n\n# comment\n\nscript Second\n[\n\tB\n]", result);
    }

    [Fact]
    public void Format_BracketOnNewLine_AlreadyFormatted()
    {
        var input = "text Foo\n[\n\thello\n]";
        var result = _formatter.Format(input);

        Assert.Equal("text Foo\n[\n\thello\n]", result);
    }

    [Fact]
    public void Format_ExtraSpacesBetweenKeywordAndName()
    {
        var input = "script    Greet [Hello!]";
        var result = _formatter.Format(input);

        Assert.Equal("script Greet\n[\n\tHello!\n]", result);
    }

    [Fact]
    public void Format_PreservesContentBetweenBlocks()
    {
        var input = "// preamble\ntext Intro [Welcome]\n// midamble\nscript Main [Start]";
        var result = _formatter.Format(input);

        Assert.Equal("// preamble\ntext Intro\n[\n\tWelcome\n]\n// midamble\nscript Main\n[\n\tStart\n]", result);
    }

    [Fact]
    public void Format_NoBlocks_ReturnsUnchanged()
    {
        var input = "# just a comment\n// another line";
        var result = _formatter.Format(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void Format_EmptyString()
    {
        var input = "";
        var result = _formatter.Format(input);

        Assert.Equal("", result);
    }

    [Fact]
    public void Format_ContentWithBlankLines()
    {
        var input = "script Gap [Line 1\n\nLine 3]";
        var result = _formatter.Format(input);

        Assert.Equal("script Gap\n[\n\tLine 1\n\t\n\tLine 3\n]", result);
    }

    [Fact]
    public void Format_CRLF_LineEndings()
    {
        var input = "script Greet [Hello!]";
        input = input.Replace("\n", "\r\n");
        var result = _formatter.Format(input);

        Assert.Equal("script Greet\n[\n\tHello!\n]", result);
    }

    [Fact]
    public void Format_ContentWithCRLF()
    {
        var input = "script Poem [Roses are red\r\nViolets are blue]";
        var result = _formatter.Format(input);

        Assert.Equal("script Poem\n[\n\tRoses are red\n\tViolets are blue\n]", result);
    }

    [Fact]
    public void Format_LeadingAndTrailingNewlinesInContent()
    {
        var input = "script Wrap [\n\n\n  hi  \n\n\n]";
        var result = _formatter.Format(input);

        Assert.Equal("script Wrap\n[\n\thi\n]", result);
    }

    [Fact]
    public void Format_BracketOnSeparateLineWithSpacing()
    {
        var input = "script Greet   \n  [  \n  Hello!  \n  ]";
        var result = _formatter.Format(input);

        Assert.Equal("script Greet\n[\n\tHello!\n]", result);
    }

    [Fact]
    public void Format_ScriptAndText_Mixed()
    {
        var input = "text ReadMe [Instructions]\n\nscript Main [Hello @world()]";
        var result = _formatter.Format(input);

        Assert.Equal("text ReadMe\n[\n\tInstructions\n]\n\nscript Main\n[\n\tHello @world()\n]", result);
    }

    [Fact]
    public void Format_BlockAtStartOfFile()
    {
        var input = "script First [A]";
        var result = _formatter.Format(input);

        Assert.Equal("script First\n[\n\tA\n]", result);
    }
}
