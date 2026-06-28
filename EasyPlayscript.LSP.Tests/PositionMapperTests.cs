using EasyPlayscript.LSP.Mapping;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace EasyPlayscript.LSP.Tests;

public class PositionMapperTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(5, 4)]
    [InlineData(10, 9)]
    public void ToLspLine_SubtractsOne(int antlr, int expected)
    {
        Assert.Equal(expected, PositionMapper.ToLspLine(antlr));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 4)]
    public void ToLspCol_ReturnsSame(int antlr, int expected)
    {
        Assert.Equal(expected, PositionMapper.ToLspCol(antlr));
    }

    [Fact]
    public void ToLspPosition_ConvertsBoth()
    {
        var pos = PositionMapper.ToLspPosition(3, 5);
        Assert.Equal(2, pos.Line);
        Assert.Equal(5, pos.Character);
    }

    [Fact]
    public void ToAbsoluteLine_NoOffset()
    {
        var block = new BlockOffset(1, 0);
        Assert.Equal(0, PositionMapper.ToAbsoluteLine(1, block));
        Assert.Equal(2, PositionMapper.ToAbsoluteLine(3, block));
    }

    [Fact]
    public void ToAbsoluteLine_WithOffset()
    {
        var block = new BlockOffset(5, 0);
        Assert.Equal(4, PositionMapper.ToAbsoluteLine(1, block));
        Assert.Equal(6, PositionMapper.ToAbsoluteLine(3, block));
    }

    [Fact]
    public void ToAbsolutePosition_CombinesCorrectly()
    {
        var block = new BlockOffset(10, 0);
        var pos = PositionMapper.ToAbsolutePosition(2, 4, block);
        Assert.Equal(10, pos.Line);
        Assert.Equal(4, pos.Character);
    }

    [Fact]
    public void ToLspRange_FormsCorrectRange()
    {
        var range = PositionMapper.ToLspRange(2, 3, 2, 8);
        Assert.Equal(1, range.Start.Line);
        Assert.Equal(3, range.Start.Character);
        Assert.Equal(1, range.End.Line);
        Assert.Equal(8, range.End.Character);
    }

    [Fact]
    public void ToLspDiagnostic_FromStructureError()
    {
        var error = new EasyPlayscript.Parsing.PlayscriptError(3, 2, "unexpected token", true);
        var uri = DocumentUri.From("/test.scpt");
        var diag = PositionMapper.ToLspDiagnostic(error, uri);

        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(2, diag.Range.Start.Character);
        Assert.Equal(3, diag.Range.End.Character);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("unexpected token", diag.Message);
        Assert.Equal("EasyPlayscript", diag.Source);
    }

    [Fact]
    public void ToLspDiagnostic_FromContentError_AppliesBlockOffset()
    {
        var error = new EasyPlayscript.Parsing.PlayscriptError(2, 1, "bad call", false);
        var block = new BlockOffset(5, 0);
        var uri = DocumentUri.From("/test.scpt");
        var diag = PositionMapper.ToLspDiagnostic(error, uri, block);

        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Character);
    }
}
