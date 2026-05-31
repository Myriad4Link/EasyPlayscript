using System.Collections.Generic;
using EasyPlayscript.Parsing;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptStructureTests
{
    [Fact]
    public void ScriptWithBlock_ExtractsRawContent()
    {
        var input = ".script(\"test\")[Hello world]";
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Single(result);
        Assert.Equal("test", result[0].name);
        Assert.Equal("script", result[0].identifier);
        Assert.Contains("Hello world", result[0].rawContent);
    }

    [Fact]
    public void TextWithBlock_ExtractsRawContent()
    {
        var input = ".text(\"intro\")[Welcome]";
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Single(result);
        Assert.Equal("text", result[0].identifier);
        Assert.Equal("intro", result[0].name);
    }

    [Fact]
    public void StandaloneCompilerCall_NoBlock()
    {
        var input = ".script(\"empty\")";
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Single(result);
        Assert.Null(result[0].rawContent);
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
        var input = ".script(\"test\")[\nline 1\nline 2\n]";
        var result = PlayscriptStructureHelper.ParseStructure(input);
        Assert.Contains("line 1", result[0].rawContent);
        Assert.Contains("line 2", result[0].rawContent);
    }

    [Fact]
    public void MalformedInput_ReportsErrors()
    {
        var input = ".script(\"test\"][Hello]";
        var (_, errors) = PlayscriptStructureHelper.ParseStructureWithErrors(input);
        Assert.NotEmpty(errors);
    }
}
