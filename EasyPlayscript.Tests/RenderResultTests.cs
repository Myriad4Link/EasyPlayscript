using EasyPlayscript.Runtime;
using Xunit;

namespace EasyPlayscript.Tests;

public class RenderResultTests
{
    [Fact]
    public void LineRenderResult_SetsText()
    {
        var result = new LineRenderResult("Hello", default, false, false, false, false, false, false);

        Assert.Equal("Hello", result.Text);
    }

    [Fact]
    public void LineRenderResult_SetsPointer()
    {
        var pointer = new ScriptPointer(1, 2, 3);
        var result = new LineRenderResult("text", pointer, false, false, false, false, false, false);

        Assert.Equal(pointer, result.Pointer);
    }

    [Fact]
    public void LineRenderResult_SetsAllFlags()
    {
        var result = new LineRenderResult("text", default,
            isLastLineOfParagraph: true,
            isLastParagraphOfPage: true,
            isLastPage: true,
            isLastLineOfPage: true,
            isLastLineOfScript: true,
            isLastParagraphOfScript: true);

        Assert.True(result.IsLastLineOfParagraph);
        Assert.True(result.IsLastParagraphOfPage);
        Assert.True(result.IsLastPage);
        Assert.True(result.IsLastLineOfPage);
        Assert.True(result.IsLastLineOfScript);
        Assert.True(result.IsLastParagraphOfScript);
    }

    [Fact]
    public void LineRenderResult_FlagsCanBeFalse()
    {
        var result = new LineRenderResult("text", default, false, false, false, false, false, false);

        Assert.False(result.IsLastLineOfParagraph);
        Assert.False(result.IsLastParagraphOfPage);
        Assert.False(result.IsLastPage);
        Assert.False(result.IsLastLineOfPage);
        Assert.False(result.IsLastLineOfScript);
        Assert.False(result.IsLastParagraphOfScript);
    }

    [Fact]
    public void LineRenderResult_IsRenderResult()
    {
        var result = new LineRenderResult("text", default, false, false, false, false, false, false);

        Assert.IsAssignableFrom<RenderResult>(result);
    }

    [Fact]
    public void ParagraphRenderResult_SetsTextAndParagraphFlags()
    {
        var result = new ParagraphRenderResult("text", default,
            isLastParagraphOfPage: true, isLastPage: false, isLastParagraphOfScript: false);

        Assert.Equal("text", result.Text);
        Assert.True(result.IsLastParagraphOfPage);
        Assert.False(result.IsLastPage);
        Assert.False(result.IsLastParagraphOfScript);
    }

    [Fact]
    public void ParagraphRenderResult_IsRenderResult()
    {
        var result = new ParagraphRenderResult("text", default, false, false, false);

        Assert.IsAssignableFrom<RenderResult>(result);
    }

    [Fact]
    public void PageRenderResult_SetsTextAndPageFlag()
    {
        var result = new PageRenderResult("text", default, isLastPage: true);

        Assert.Equal("text", result.Text);
        Assert.True(result.IsLastPage);
    }

    [Fact]
    public void PageRenderResult_IsRenderResult()
    {
        var result = new PageRenderResult("text", default, false);

        Assert.IsAssignableFrom<RenderResult>(result);
    }

    [Fact]
    public void AllSubtypes_AreSealed()
    {
        Assert.True(typeof(LineRenderResult).IsSealed);
        Assert.True(typeof(ParagraphRenderResult).IsSealed);
        Assert.True(typeof(PageRenderResult).IsSealed);
        Assert.True(typeof(SegmentRenderResult).IsSealed);
    }

    [Fact]
    public void BaseRenderResult_IsAbstract()
    {
        Assert.True(typeof(RenderResult).IsAbstract);
    }

    // ─── SegmentRenderResult ────────────────────────────────────────────────

    [Fact]
    public void SegmentRenderResult_SetsText()
    {
        var result = new SegmentRenderResult("Hello", default, false, false, false, false, false);

        Assert.Equal("Hello", result.Text);
    }

    [Fact]
    public void SegmentRenderResult_SetsPointer()
    {
        var pointer = new ScriptPointer(1, 2, 3);
        var result = new SegmentRenderResult("text", pointer, false, false, false, false, false);

        Assert.Equal(pointer, result.Pointer);
    }

    [Fact]
    public void SegmentRenderResult_SetsIsLastPage()
    {
        var result = new SegmentRenderResult("text", default, isLastPage: true, false, false, false, false);

        Assert.True(result.IsLastPage);
    }

    [Fact]
    public void SegmentRenderResult_SetsSegmentFlags_AllTrue()
    {
        var result = new SegmentRenderResult("text", default,
            isLastPage: true,
            isLastSegmentOfLine: true,
            isLastSegmentOfParagraph: true,
            isLastSegmentOfPage: true,
            isLastSegmentOfScript: true);

        Assert.True(result.IsLastSegmentOfLine);
        Assert.True(result.IsLastSegmentOfParagraph);
        Assert.True(result.IsLastSegmentOfPage);
        Assert.True(result.IsLastSegmentOfScript);
    }

    [Fact]
    public void SegmentRenderResult_SegmentFlags_AllFalse()
    {
        var result = new SegmentRenderResult("text", default, false, false, false, false, false);

        Assert.False(result.IsLastSegmentOfLine);
        Assert.False(result.IsLastSegmentOfParagraph);
        Assert.False(result.IsLastSegmentOfPage);
        Assert.False(result.IsLastSegmentOfScript);
    }

    [Fact]
    public void SegmentRenderResult_IsRenderResult()
    {
        var result = new SegmentRenderResult("text", default, false, false, false, false, false);

        Assert.IsAssignableFrom<RenderResult>(result);
    }
}
