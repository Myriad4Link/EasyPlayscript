using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EasyPlayscript.DataModel;
using EasyPlayscript.Runtime;
using Xunit;

namespace EasyPlayscript.Tests;

public class ScriptNavigationTests
{
    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static ScriptNavigator CreateNav(ScriptBlock block) => new(block);
    private static ScriptBlock Block(params Page[] pages) => new() { Pages = new List<Page>(pages) };
    private static Page Pg(params Paragraph[] paragraphs) => new() { Paragraphs = new List<Paragraph>(paragraphs) };
    private static Paragraph Para(params Line[] lines) => new() { Lines = new List<Line>(lines) };
    private static Line Li(params LineItem[] items) => new() { Segments = new List<Segment> { new() { Items = new List<LineItem>(items) } } };
    private static TextItem T(string text) => new(text);

    private static string RenderLine(Line line)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var segment in line.Segments)
        foreach (var item in segment.Items)
        {
            switch (item)
            {
                case TextItem textItem:
                    sb.Append(textItem.Text);
                    break;
                case ConsumerCallItem call:
                    call.Result = $"[{call.Identifier}]";
                    sb.Append(call.Result);
                    break;
            }
        }
        return sb.ToString();
    }

    // ─── Phase 2: Pointer State ─────────────────────────────────────────────────

    [Fact]
    public void Pointer_InitialState_IsZeroZeroZero()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("Hello"))))));

        Assert.Equal(new ScriptPointer(0, 0, 0), nav.Pointer);
    }

    [Fact]
    public void Pointer_AfterRenderNextLine_ReturnsNewPointer()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("A")), Li(T("B"))))));

        nav.RenderNextLine(RenderLine);

        Assert.Equal(new ScriptPointer(0, 0, 1), nav.Pointer);
    }

    // ─── Phase 3: RenderNextLine ────────────────────────────────────────────────

    [Fact]
    public void RenderNextLine_EmptyScript_ReturnsNull()
    {
        var nav = CreateNav(Block());

        Assert.Null(nav.RenderNextLine(RenderLine));
    }

    [Fact]
    public void RenderNextLine_SingleTextItem_ReturnsText()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("Hello"))))));

        Assert.Equal("Hello", nav.RenderNextLine(RenderLine)!.Text);
    }

    [Fact]
    public void RenderNextLine_MultipleTextItems_Concatenates()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("A"), T("B"))))));

        Assert.Equal("AB", nav.RenderNextLine(RenderLine)!.Text);
    }

    [Fact]
    public void RenderNextLine_DispatchesConsumerCall_AppendsResult()
    {
        var call = new ConsumerCallItem("get_name", new List<ArgumentValue>());
        var nav = CreateNav(Block(Pg(Para(Li(T("Hi, "), call, T("!"))))));

        var result = nav.RenderNextLine(RenderLine)!;

        Assert.Contains("[get_name]", result.Text);
        Assert.Equal("Hi, [get_name]!", result.Text);
    }

    [Fact]
    public void RenderNextLine_AdvancesPointer()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("A")), Li(T("B")), Li(T("C"))))));

        nav.RenderNextLine(RenderLine);
        nav.RenderNextLine(RenderLine);

        Assert.Equal(new ScriptPointer(0, 0, 2), nav.Pointer);
    }

    [Fact]
    public void RenderNextLine_CrossesParagraphBoundary()
    {
        var nav = CreateNav(Block(Pg(
            Para(Li(T("p1l1"))),
            Para(Li(T("p2l1")))
        )));

        nav.RenderNextLine(RenderLine);

        Assert.Equal(new ScriptPointer(0, 1, 0), nav.Pointer);
    }

    [Fact]
    public void RenderNextLine_CrossesPageBoundary()
    {
        var nav = CreateNav(Block(
            Pg(Para(Li(T("page1")))),
            Pg(Para(Li(T("page2"))))
        ));

        nav.RenderNextLine(RenderLine);

        Assert.Equal(new ScriptPointer(1, 0, 0), nav.Pointer);
    }

    [Fact]
    public void RenderNextLine_AtEndOfScript_ReturnsNull()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("only"))))));

        nav.RenderNextLine(RenderLine);

        Assert.Null(nav.RenderNextLine(RenderLine));
    }

    // ─── RenderNextLine: Result Flags ───────────────────────────────────────────

    [Fact]
    public void RenderNextLine_Result_PointerMatchesPreAdvancePosition()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("A")), Li(T("B"))))));

        var r1 = nav.RenderNextLine(RenderLine)!;
        var r2 = nav.RenderNextLine(RenderLine)!;

        Assert.Equal(new ScriptPointer(0, 0, 0), r1.Pointer);
        Assert.Equal(new ScriptPointer(0, 0, 1), r2.Pointer);
    }

    [Fact]
    public void RenderNextLine_Result_IsLastLineOfParagraph_TrueAtBoundary()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("A")), Li(T("B"))))));

        var r1 = nav.RenderNextLine(RenderLine)!;
        var r2 = nav.RenderNextLine(RenderLine)!;

        Assert.False(r1.IsLastLineOfParagraph);
        Assert.True(r2.IsLastLineOfParagraph);
    }

    [Fact]
    public void RenderNextLine_Result_IsLastLineOfParagraph_FalseWhenNotAtBoundary()
    {
        var nav = CreateMultiLevelNav();

        var r = nav.RenderNextLine(RenderLine)!;

        Assert.False(r.IsLastLineOfParagraph);
    }

    [Fact]
    public void RenderNextLine_Result_IsLastLineOfPage_TrueAtPageBoundary()
    {
        var nav = CreateMultiLevelNav();

        // p0p0l0, p0p0l1, p0p1l0 (last line of page 0)
        nav.RenderNextLine(RenderLine);
        nav.RenderNextLine(RenderLine);
        var r = nav.RenderNextLine(RenderLine)!;

        Assert.True(r.IsLastLineOfPage);
    }

    [Fact]
    public void RenderNextLine_Result_IsLastLineOfScript_TrueAtEnd()
    {
        var nav = CreateMultiLevelNav();

        // Skip to last line
        nav.JumpTo(new ScriptPointer(1, 0, 0));
        var r = nav.RenderNextLine(RenderLine)!;

        Assert.True(r.IsLastLineOfScript);
    }

    [Fact]
    public void RenderNextLine_Result_IsLastPage_TrueOnLastPage()
    {
        var nav = CreateMultiLevelNav();

        nav.JumpTo(new ScriptPointer(1, 0, 0));
        var r = nav.RenderNextLine(RenderLine)!;

        Assert.True(r.IsLastPage);
    }

    [Fact]
    public void RenderNextLine_Result_AllFlagsFalseOnFirstLine()
    {
        var nav = CreateMultiLevelNav();

        var r = nav.RenderNextLine(RenderLine)!;

        Assert.False(r.IsLastLineOfParagraph);
        Assert.False(r.IsLastParagraphOfPage);
        Assert.False(r.IsLastPage);
        Assert.False(r.IsLastLineOfPage);
        Assert.False(r.IsLastLineOfScript);
        Assert.False(r.IsLastParagraphOfScript);
    }

    [Fact]
    public void RenderNextLine_Result_IsLastParagraphOfPage_TrueAtBoundary()
    {
        var nav = CreateMultiLevelNav();

        // p0p1l0 — last paragraph of page 0
        nav.JumpTo(new ScriptPointer(0, 1, 0));
        var r = nav.RenderNextLine(RenderLine)!;

        Assert.True(r.IsLastParagraphOfPage);
    }

    [Fact]
    public void RenderNextLine_Result_IsLastParagraphOfScript_TrueAtEnd()
    {
        var nav = CreateMultiLevelNav();

        nav.JumpTo(new ScriptPointer(1, 0, 0));
        var r = nav.RenderNextLine(RenderLine)!;

        Assert.True(r.IsLastParagraphOfScript);
    }

    // ─── Phase 4: RenderNextParagraph ───────────────────────────────────────────

    [Fact]
    public void RenderNextParagraph_EmptyScript_ReturnsNull()
    {
        var nav = CreateNav(Block());

        Assert.Null(nav.RenderNextParagraph(RenderLine));
    }

    [Fact]
    public void RenderNextParagraph_SingleLine_ReturnsThatLine()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("Hello"))))));

        Assert.Equal("Hello", nav.RenderNextParagraph(RenderLine)!.Text);
    }

    [Fact]
    public void RenderNextParagraph_MultipleLines_JoinsWithNewline()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("line1")), Li(T("line2")), Li(T("line3"))))));

        var result = nav.RenderNextParagraph(RenderLine)!.Text;

        var lines = result.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        Assert.Equal(3, lines.Length);
        Assert.Equal("line1", lines[0]);
        Assert.Equal("line2", lines[1]);
        Assert.Equal("line3", lines[2]);
    }

    [Fact]
    public void RenderNextParagraph_DispatchesConsumerCalls()
    {
        var call = new ConsumerCallItem("get_name", new List<ArgumentValue>());
        var nav = CreateNav(Block(Pg(Para(Li(T("Hi "), call)))));

        var result = nav.RenderNextParagraph(RenderLine)!.Text;

        Assert.Contains("[get_name]", result);
    }

    [Fact]
    public void RenderNextParagraph_AdvancesPastParagraph()
    {
        var nav = CreateNav(Block(Pg(
            Para(Li(T("p1l1")), Li(T("p1l2"))),
            Para(Li(T("p2l1")))
        )));

        nav.RenderNextParagraph(RenderLine);

        Assert.Equal(new ScriptPointer(0, 1, 0), nav.Pointer);
    }

    [Fact]
    public void RenderNextParagraph_CrossesPageBoundary()
    {
        var nav = CreateNav(Block(
            Pg(Para(Li(T("page1para1")))),
            Pg(Para(Li(T("page2para1"))))
        ));

        nav.RenderNextParagraph(RenderLine);

        Assert.Equal(new ScriptPointer(1, 0, 0), nav.Pointer);
    }

    [Fact]
    public void RenderNextParagraph_AtEnd_ReturnsNull()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("only"))))));

        nav.RenderNextParagraph(RenderLine);

        Assert.Null(nav.RenderNextParagraph(RenderLine));
    }

    // ─── RenderNextParagraph: Result Flags ──────────────────────────────────────

    [Fact]
    public void RenderNextParagraph_Result_HasParagraphAndPageFlags()
    {
        var nav = CreateMultiLevelNav();

        // First paragraph: not last of page, not last of script, not last page
        var r1 = nav.RenderNextParagraph(RenderLine)!;
        Assert.False(r1.IsLastParagraphOfPage);
        Assert.False(r1.IsLastParagraphOfScript);
        Assert.False(r1.IsLastPage);

        // Second paragraph of page 0: last paragraph of page, not last of script
        var r2 = nav.RenderNextParagraph(RenderLine)!;
        Assert.True(r2.IsLastParagraphOfPage);
        Assert.False(r2.IsLastParagraphOfScript);
        Assert.False(r2.IsLastPage);

        // First (only) paragraph of page 1: last page
        var r3 = nav.RenderNextParagraph(RenderLine)!;
        Assert.True(r3.IsLastParagraphOfPage);
        Assert.True(r3.IsLastParagraphOfScript);
        Assert.True(r3.IsLastPage);
    }

    [Fact]
    public void RenderNextParagraph_Result_IsNotLineResult()
    {
        var nav = CreateMultiLevelNav();

        var r = nav.RenderNextParagraph(RenderLine)!;

        // Paragraph result is its own type — line-specific flags are not on this type
        Assert.IsType<ParagraphRenderResult>(r);
        Assert.IsNotType<LineRenderResult>(r);
    }

    [Fact]
    public void RenderNextParagraph_Result_PointerIsPreAdvance()
    {
        var nav = CreateMultiLevelNav();

        var r1 = nav.RenderNextParagraph(RenderLine)!;
        Assert.Equal(new ScriptPointer(0, 0, 0), r1.Pointer);

        var r2 = nav.RenderNextParagraph(RenderLine)!;
        Assert.Equal(new ScriptPointer(0, 1, 0), r2.Pointer);
    }

    // ─── Phase 5: RenderNextPage ────────────────────────────────────────────────

    [Fact]
    public void RenderNextPage_EmptyScript_ReturnsNull()
    {
        var nav = CreateNav(Block());

        Assert.Null(nav.RenderNextPage(RenderLine));
    }

    [Fact]
    public void RenderNextPage_SingleParagraph_ReturnsThatParagraph()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("Hello"))))));

        Assert.Equal("Hello", nav.RenderNextPage(RenderLine)!.Text);
    }

    [Fact]
    public void RenderNextPage_MultipleParagraphs_JoinsWithBlankLine()
    {
        var nav = CreateNav(Block(Pg(
            Para(Li(T("para1line1")), Li(T("para1line2"))),
            Para(Li(T("para2line1")))
        )));

        var result = nav.RenderNextPage(RenderLine)!.Text;

        var paragraphs = result.Split(new[] { Environment.NewLine + Environment.NewLine }, StringSplitOptions.None);
        Assert.Equal(2, paragraphs.Length);
        Assert.Contains("para1line1", paragraphs[0]);
        Assert.Contains("para1line2", paragraphs[0]);
        Assert.Equal("para2line1", paragraphs[1]);
    }

    [Fact]
    public void RenderNextPage_DispatchesConsumerCalls()
    {
        var call = new ConsumerCallItem("get_name", new List<ArgumentValue>());
        var nav = CreateNav(Block(Pg(Para(Li(T("Hi "), call)))));

        var result = nav.RenderNextPage(RenderLine)!.Text;

        Assert.Contains("[get_name]", result);
    }

    [Fact]
    public void RenderNextPage_AdvancesPastPage()
    {
        var nav = CreateNav(Block(
            Pg(Para(Li(T("page1")))),
            Pg(Para(Li(T("page2"))))
        ));

        nav.RenderNextPage(RenderLine);

        Assert.Equal(new ScriptPointer(1, 0, 0), nav.Pointer);
    }

    [Fact]
    public void RenderNextPage_AtEnd_ReturnsNull()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("only"))))));

        nav.RenderNextPage(RenderLine);

        Assert.Null(nav.RenderNextPage(RenderLine));
    }

    // ─── RenderNextPage: Result Flags ───────────────────────────────────────────

    [Fact]
    public void RenderNextPage_Result_IsNotLineOrParagraphResult()
    {
        var nav = CreateMultiLevelNav();

        // Page 0: not last page
        var r1 = nav.RenderNextPage(RenderLine)!;
        Assert.IsType<PageRenderResult>(r1);
        Assert.IsNotType<LineRenderResult>(r1);
        Assert.IsNotType<ParagraphRenderResult>(r1);
        Assert.False(r1.IsLastPage);

        // Page 1: last page
        var r2 = nav.RenderNextPage(RenderLine)!;
        Assert.True(r2.IsLastPage);
    }

    [Fact]
    public void RenderNextPage_Result_PointerIsPreAdvance()
    {
        var nav = CreateMultiLevelNav();

        var r1 = nav.RenderNextPage(RenderLine)!;
        Assert.Equal(new ScriptPointer(0, 0, 0), r1.Pointer);

        var r2 = nav.RenderNextPage(RenderLine)!;
        Assert.Equal(new ScriptPointer(1, 0, 0), r2.Pointer);
    }

    // ─── Phase 6: IsLast* Boundary Checks ──────────────────────────────────────
    // Script: Page0 has 2 paragraphs [2 lines, 1 line], Page1 has 1 paragraph [1 line]

    private static ScriptNavigator CreateMultiLevelNav()
    {
        return CreateNav(Block(
            Pg(
                Para(Li(T("p0p0l0")), Li(T("p0p0l1"))),
                Para(Li(T("p0p1l0")))
            ),
            Pg(
                Para(Li(T("p1p0l0")))
            )
        ));
    }

    [Fact]
    public void IsLastLineOfParagraph_WhenTrue()
    {
        var nav = CreateMultiLevelNav();
        nav.JumpTo(new ScriptPointer(0, 0, 1));

        Assert.True(nav.IsLastLineOfParagraph);
    }

    [Fact]
    public void IsLastLineOfParagraph_WhenFalse()
    {
        var nav = CreateMultiLevelNav();
        nav.JumpTo(new ScriptPointer(0, 0, 0));

        Assert.False(nav.IsLastLineOfParagraph);
    }

    [Fact]
    public void IsLastLineOfPage_WhenTrue()
    {
        var nav = CreateMultiLevelNav();
        nav.JumpTo(new ScriptPointer(0, 1, 0));

        Assert.True(nav.IsLastLineOfPage);
    }

    [Fact]
    public void IsLastLineOfPage_WhenFalse()
    {
        var nav = CreateMultiLevelNav();
        nav.JumpTo(new ScriptPointer(0, 0, 1));

        Assert.False(nav.IsLastLineOfPage);
    }

    [Fact]
    public void IsLastLineOfScript_WhenTrue()
    {
        var nav = CreateMultiLevelNav();
        nav.JumpTo(new ScriptPointer(1, 0, 0));

        Assert.True(nav.IsLastLineOfScript);
    }

    [Fact]
    public void IsLastLineOfScript_WhenFalse()
    {
        var nav = CreateMultiLevelNav();
        nav.JumpTo(new ScriptPointer(0, 0, 0));

        Assert.False(nav.IsLastLineOfScript);
    }

    [Fact]
    public void IsLastParagraphOfPage_WhenTrue()
    {
        var nav = CreateMultiLevelNav();
        nav.JumpTo(new ScriptPointer(0, 1, 0));

        Assert.True(nav.IsLastParagraphOfPage);
    }

    [Fact]
    public void IsLastParagraphOfPage_WhenFalse()
    {
        var nav = CreateMultiLevelNav();
        nav.JumpTo(new ScriptPointer(0, 0, 0));

        Assert.False(nav.IsLastParagraphOfPage);
    }

    [Fact]
    public void IsLastParagraphOfScript_WhenTrue()
    {
        var nav = CreateMultiLevelNav();
        nav.JumpTo(new ScriptPointer(1, 0, 0));

        Assert.True(nav.IsLastParagraphOfScript);
    }

    [Fact]
    public void IsLastParagraphOfScript_WhenFalse()
    {
        var nav = CreateMultiLevelNav();
        nav.JumpTo(new ScriptPointer(0, 0, 0));

        Assert.False(nav.IsLastParagraphOfScript);
    }

    [Fact]
    public void IsLastPage_WhenTrue()
    {
        var nav = CreateMultiLevelNav();
        nav.JumpTo(new ScriptPointer(1, 0, 0));

        Assert.True(nav.IsLastPage);
    }

    [Fact]
    public void IsLastPage_WhenFalse()
    {
        var nav = CreateMultiLevelNav();
        nav.JumpTo(new ScriptPointer(0, 0, 0));

        Assert.False(nav.IsLastPage);
    }

    [Fact]
    public void AllIsLast_OnEmptyScript_ReturnsTrue()
    {
        var nav = CreateNav(Block());

        Assert.True(nav.IsLastLineOfParagraph);
        Assert.True(nav.IsLastLineOfPage);
        Assert.True(nav.IsLastLineOfScript);
        Assert.True(nav.IsLastParagraphOfPage);
        Assert.True(nav.IsLastParagraphOfScript);
        Assert.True(nav.IsLastPage);
    }

    [Fact]
    public void AllIsLast_AfterRenderingAllLines_DoesNotThrow()
    {
        var nav = CreateMultiLevelNav();

        while (nav.RenderNextLine(RenderLine) is { })
        {
        }

        Assert.True(nav.IsLastLineOfParagraph);
        Assert.True(nav.IsLastLineOfPage);
        Assert.True(nav.IsLastLineOfScript);
        Assert.True(nav.IsLastParagraphOfPage);
        Assert.True(nav.IsLastParagraphOfScript);
        Assert.True(nav.IsLastPage);
    }

    // ─── Phase 7: JumpTo + Reset ────────────────────────────────────────────────

    [Fact]
    public void JumpTo_ValidPointer_SetsPosition()
    {
        var nav = CreateMultiLevelNav();

        nav.JumpTo(new ScriptPointer(1, 0, 0));

        Assert.Equal(new ScriptPointer(1, 0, 0), nav.Pointer);
    }

    [Fact]
    public void JumpTo_InvalidPageIndex_ThrowsArgumentOutOfRange()
    {
        var nav = CreateMultiLevelNav();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            nav.JumpTo(new ScriptPointer(99, 0, 0)));
    }

    [Fact]
    public void JumpTo_InvalidParagraphIndex_ThrowsArgumentOutOfRange()
    {
        var nav = CreateMultiLevelNav();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            nav.JumpTo(new ScriptPointer(0, 99, 0)));
    }

    [Fact]
    public void JumpTo_InvalidLineIndex_ThrowsArgumentOutOfRange()
    {
        var nav = CreateMultiLevelNav();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            nav.JumpTo(new ScriptPointer(0, 0, 99)));
    }

    [Fact]
    public void JumpTo_ThenRenderNextLine_ReturnsCorrectLine()
    {
        var nav = CreateMultiLevelNav();

        nav.JumpTo(new ScriptPointer(1, 0, 0));
        var result = nav.RenderNextLine(RenderLine);

        Assert.Equal("p1p0l0", result!.Text);
    }

    [Fact]
    public void Reset_ReturnsToStart()
    {
        var nav = CreateMultiLevelNav();

        nav.RenderNextLine(RenderLine);
        nav.RenderNextLine(RenderLine);
        nav.Reset();

        Assert.Equal(new ScriptPointer(0, 0, 0), nav.Pointer);
        Assert.Equal("p0p0l0", nav.RenderNextLine(RenderLine)!.Text);
    }

    [Fact]
    public void Reset_ThenPointer_IsZeroZeroZero()
    {
        var nav = CreateMultiLevelNav();

        nav.JumpTo(new ScriptPointer(1, 0, 0));
        nav.Reset();

        Assert.Equal(new ScriptPointer(0, 0, 0), nav.Pointer);
    }

    // ─── Integration: Full render sequence ──────────────────────────────────────

    [Fact]
    public void FullSequence_RenderAllLines_ReturnsExpectedContent()
    {
        var nav = CreateMultiLevelNav();

        var results = new List<LineRenderResult>();
        while (nav.RenderNextLine(RenderLine) is { } line)
            results.Add(line);

        Assert.Equal(4, results.Count);
        Assert.Equal("p0p0l0", results[0].Text);
        Assert.Equal("p0p0l1", results[1].Text);
        Assert.Equal("p0p1l0", results[2].Text);
        Assert.Equal("p1p0l0", results[3].Text);
    }

    [Fact]
    public void FullSequence_RenderAllParagraphs_ReturnsExpectedContent()
    {
        var nav = CreateMultiLevelNav();

        var results = new List<ParagraphRenderResult>();
        while (nav.RenderNextParagraph(RenderLine) is { } para)
            results.Add(para);

        Assert.Equal(3, results.Count);
        Assert.Contains("p0p0l0", results[0].Text);
        Assert.Contains("p0p0l1", results[0].Text);
        Assert.Equal("p0p1l0", results[1].Text);
        Assert.Equal("p1p0l0", results[2].Text);
    }

    [Fact]
    public void FullSequence_RenderAllPages_ReturnsExpectedContent()
    {
        var nav = CreateMultiLevelNav();

        var results = new List<PageRenderResult>();
        while (nav.RenderNextPage(RenderLine) is { } page)
            results.Add(page);

        Assert.Equal(2, results.Count);
        Assert.Contains("p0p0l0", results[0].Text);
        Assert.Contains("p0p1l0", results[0].Text);
        Assert.Equal("p1p0l0", results[1].Text);
    }

    [Fact]
    public void FullSequence_RenderAllLines_FlagsAreCorrectAtEachStep()
    {
        var nav = CreateMultiLevelNav();

        var results = new List<LineRenderResult>();
        while (nav.RenderNextLine(RenderLine) is { } line)
            results.Add(line);

        // p0p0l0 — first line, first paragraph, first page
        Assert.Equal(new ScriptPointer(0, 0, 0), results[0].Pointer);
        Assert.False(results[0].IsLastLineOfParagraph);
        Assert.False(results[0].IsLastPage);

        // p0p0l1 — last line of paragraph 0
        Assert.Equal(new ScriptPointer(0, 0, 1), results[1].Pointer);
        Assert.True(results[1].IsLastLineOfParagraph);
        Assert.False(results[1].IsLastParagraphOfPage);
        Assert.False(results[1].IsLastPage);

        // p0p1l0 — last line of page 0
        Assert.Equal(new ScriptPointer(0, 1, 0), results[2].Pointer);
        Assert.True(results[2].IsLastLineOfParagraph);
        Assert.True(results[2].IsLastParagraphOfPage);
        Assert.True(results[2].IsLastLineOfPage);
        Assert.False(results[2].IsLastPage);

        // p1p0l0 — last line of script
        Assert.Equal(new ScriptPointer(1, 0, 0), results[3].Pointer);
        Assert.True(results[3].IsLastLineOfParagraph);
        Assert.True(results[3].IsLastParagraphOfPage);
        Assert.True(results[3].IsLastPage);
        Assert.True(results[3].IsLastLineOfPage);
        Assert.True(results[3].IsLastLineOfScript);
        Assert.True(results[3].IsLastParagraphOfScript);
    }

    // ─── Async Navigation Tests ──────────────────────────────────────────────

    private static Task<string> RenderLineAsync(Line line)
    {
        return Task.FromResult(RenderLine(line));
    }

    [Fact]
    public async Task RenderNextLineAsync_EmptyScript_ReturnsNull()
    {
        var nav = CreateNav(Block());
        Assert.Null(await nav.RenderNextLineAsync(RenderLineAsync));
    }

    [Fact]
    public async Task RenderNextLineAsync_SingleTextItem_ReturnsText()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("Hello"))))));
        Assert.Equal("Hello", (await nav.RenderNextLineAsync(RenderLineAsync))!.Text);
    }

    [Fact]
    public async Task RenderNextLineAsync_MultipleItems_Concatenates()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("A"), T("B"))))));
        Assert.Equal("AB", (await nav.RenderNextLineAsync(RenderLineAsync))!.Text);
    }

    [Fact]
    public async Task RenderNextLineAsync_AdvancesPointer()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("A")), Li(T("B"))))));
        await nav.RenderNextLineAsync(RenderLineAsync);
        Assert.Equal(new ScriptPointer(0, 0, 1), nav.Pointer);
    }

    [Fact]
    public async Task RenderNextParagraphAsync_MultipleLines_JoinsWithNewline()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("Line1")), Li(T("Line2"))))));
        var result = await nav.RenderNextParagraphAsync(RenderLineAsync);
        Assert.Contains("Line1", result!.Text);
        Assert.Contains("Line2", result.Text);
    }

    [Fact]
    public async Task RenderNextPageAsync_MultipleParagraphs_SeparatesWithBlankLine()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("P1"))), Para(Li(T("P2"))))));
        var result = await nav.RenderNextPageAsync(RenderLineAsync);
        Assert.Contains("P1", result!.Text);
        Assert.Contains("P2", result.Text);
    }

    [Fact]
    public async Task RenderNextLineAsync_EmptyScript_ReturnsNullAndDoesNotAdvance()
    {
        var nav = CreateNav(Block());
        Assert.Null(await nav.RenderNextLineAsync(RenderLineAsync));
        Assert.Equal(new ScriptPointer(0, 0, 0), nav.Pointer);
    }

    [Fact]
    public async Task RenderNextLineAsync_FullSequence_MatchesSync()
    {
        var syncNav = CreateMultiLevelNav();
        var asyncNav = CreateMultiLevelNav();

        var syncResults = new List<string>();
        while (syncNav.RenderNextLine(RenderLine) is { } line)
            syncResults.Add(line.Text);

        var asyncResults = new List<string>();
        while (await asyncNav.RenderNextLineAsync(RenderLineAsync) is { } line)
            asyncResults.Add(line.Text);

        Assert.Equal(syncResults, asyncResults);
    }

    [Fact]
    public async Task RenderNextLineAsync_Result_FlagsMatchSync()
    {
        var syncNav = CreateMultiLevelNav();
        var asyncNav = CreateMultiLevelNav();

        while (true)
        {
            var syncResult = syncNav.RenderNextLine(RenderLine);
            var asyncResult = await asyncNav.RenderNextLineAsync(RenderLineAsync);

            if (syncResult is null)
            {
                Assert.Null(asyncResult);
                break;
            }

            Assert.Equal(syncResult.IsLastLineOfParagraph, asyncResult!.IsLastLineOfParagraph);
            Assert.Equal(syncResult.IsLastPage, asyncResult.IsLastPage);
            Assert.Equal(syncResult.Pointer, asyncResult.Pointer);
        }
    }

    [Fact]
    public async Task JumpTo_ThenRenderNextLineAsync_StartsAtNewPosition()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("First")), Li(T("Second"))))));
        nav.JumpTo(new ScriptPointer(0, 0, 1));
        Assert.Equal("Second", (await nav.RenderNextLineAsync(RenderLineAsync))!.Text);
    }

    // ─── RenderNextLineSegment: Helpers ─────────────────────────────────────

    private static Line MultiSeg(params Segment[] segments) => new() { Segments = new List<Segment>(segments) };
    private static Segment Seg(params LineItem[] items) => new() { Items = new List<LineItem>(items) };

    private static string RenderSegment(Segment segment)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var item in segment.Items)
        {
            switch (item)
            {
                case TextItem textItem:
                    sb.Append(textItem.Text);
                    break;
                case ConsumerCallItem call:
                    call.Result = $"[{call.Identifier}]";
                    sb.Append(call.Result);
                    break;
            }
        }
        return sb.ToString();
    }

    private static Task<string> RenderSegmentAsync(Segment segment) => Task.FromResult(RenderSegment(segment));

    // ─── RenderNextLineSegment: Basic Navigation ────────────────────────────

    [Fact]
    public void RenderNextLineSegment_EmptyScript_ReturnsNull()
    {
        var nav = CreateNav(Block());
        Assert.Null(nav.RenderNextLineSegment(RenderSegment));
    }

    [Fact]
    public void RenderNextLineSegment_SingleSegment_ReturnsText()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("Hello"))))));
        Assert.Equal("Hello", nav.RenderNextLineSegment(RenderSegment)!.Text);
    }

    [Fact]
    public void RenderNextLineSegment_SingleSegment_AdvancesOnNextCall()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("A")), Li(T("B"))))));
        nav.RenderNextLineSegment(RenderSegment);
        Assert.Equal(new ScriptPointer(0, 0, 0), nav.Pointer);
        nav.RenderNextLineSegment(RenderSegment);
        Assert.Equal(new ScriptPointer(0, 0, 1), nav.Pointer);
    }

    [Fact]
    public void RenderNextLineSegment_TwoSegments_ReturnsFirst()
    {
        var nav = CreateNav(Block(Pg(Para(MultiSeg(Seg(T("Hello, ")), Seg(T("World!")))))));
        var result = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.Equal("Hello, ", result.Text);
    }

    [Fact]
    public void RenderNextLineSegment_TwoSegments_ReturnsSecond()
    {
        var nav = CreateNav(Block(Pg(Para(MultiSeg(Seg(T("Hello, ")), Seg(T("World!")))))));
        nav.RenderNextLineSegment(RenderSegment);
        var result = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.Equal("World!", result.Text);
    }

    [Fact]
    public void RenderNextLineSegment_ThreeSegments_ReturnsAll()
    {
        var nav = CreateNav(Block(Pg(Para(MultiSeg(Seg(T("A")), Seg(T("B")), Seg(T("C")))))));
        Assert.Equal("A", nav.RenderNextLineSegment(RenderSegment)!.Text);
        Assert.Equal("B", nav.RenderNextLineSegment(RenderSegment)!.Text);
        Assert.Equal("C", nav.RenderNextLineSegment(RenderSegment)!.Text);
    }

    [Fact]
    public void RenderNextLineSegment_CrossesLineBoundary()
    {
        var nav = CreateNav(Block(Pg(Para(
            MultiSeg(Seg(T("A1")), Seg(T("A2"))),
            Li(T("B"))
        ))));
        nav.RenderNextLineSegment(RenderSegment);
        nav.RenderNextLineSegment(RenderSegment);
        var result = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.Equal("B", result.Text);
    }

    [Fact]
    public void RenderNextLineSegment_CrossesParagraphBoundary()
    {
        var nav = CreateNav(Block(Pg(
            Para(MultiSeg(Seg(T("A1")), Seg(T("A2")))),
            Para(Li(T("B")))
        )));
        nav.RenderNextLineSegment(RenderSegment);
        nav.RenderNextLineSegment(RenderSegment);
        var result = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.Equal("B", result.Text);
    }

    [Fact]
    public void RenderNextLineSegment_CrossesPageBoundary()
    {
        var nav = CreateNav(Block(
            Pg(Para(MultiSeg(Seg(T("A1")), Seg(T("A2"))))),
            Pg(Para(Li(T("B"))))
        ));
        nav.RenderNextLineSegment(RenderSegment);
        nav.RenderNextLineSegment(RenderSegment);
        var result = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.Equal("B", result.Text);
    }

    [Fact]
    public void RenderNextLineSegment_AtEndOfScript_ReturnsNull()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("only"))))));
        nav.RenderNextLineSegment(RenderSegment);
        Assert.Null(nav.RenderNextLineSegment(RenderSegment));
    }

    [Fact]
    public void RenderNextLineSegment_DispatchesConsumerCall()
    {
        var call = new ConsumerCallItem("get_name", new List<ArgumentValue>());
        var nav = CreateNav(Block(Pg(Para(MultiSeg(Seg(T("Hi, ")), Seg(call, T("!")))))));
        nav.RenderNextLineSegment(RenderSegment);
        var result = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.Equal("[get_name]!", result.Text);
    }

    // ─── RenderNextLineSegment: Segment Flags ───────────────────────────────

    [Fact]
    public void RenderNextLineSegment_IsLastSegmentOfLine_TrueOnLastSegment()
    {
        var nav = CreateNav(Block(Pg(Para(MultiSeg(Seg(T("A")), Seg(T("B")))))));
        nav.RenderNextLineSegment(RenderSegment);
        var result = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.True(result.IsLastSegmentOfLine);
    }

    [Fact]
    public void RenderNextLineSegment_IsLastSegmentOfLine_FalseOnFirst()
    {
        var nav = CreateNav(Block(Pg(Para(MultiSeg(Seg(T("A")), Seg(T("B")))))));
        var result = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.False(result.IsLastSegmentOfLine);
    }

    [Fact]
    public void RenderNextLineSegment_IsLastSegmentOfParagraph_TrueWhenLastSegOfLastLine()
    {
        var nav = CreateNav(Block(Pg(Para(
            MultiSeg(Seg(T("A")), Seg(T("B"))),
            MultiSeg(Seg(T("C")), Seg(T("D")))
        ))));
        nav.RenderNextLineSegment(RenderSegment);
        nav.RenderNextLineSegment(RenderSegment);
        nav.RenderNextLineSegment(RenderSegment);
        var result = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.True(result.IsLastSegmentOfParagraph);
    }

    [Fact]
    public void RenderNextLineSegment_IsLastSegmentOfPage_TrueAtPageEnd()
    {
        var nav = CreateNav(Block(
            Pg(Para(MultiSeg(Seg(T("A")), Seg(T("B"))))),
            Pg(Para(Li(T("C"))))
        ));
        nav.RenderNextLineSegment(RenderSegment);
        var result = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.True(result.IsLastSegmentOfPage);
    }

    [Fact]
    public void RenderNextLineSegment_IsLastSegmentOfScript_TrueAtScriptEnd()
    {
        var nav = CreateNav(Block(Pg(Para(MultiSeg(Seg(T("A")), Seg(T("B")))))));
        nav.RenderNextLineSegment(RenderSegment);
        var result = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.True(result.IsLastSegmentOfScript);
    }

    [Fact]
    public void RenderNextLineSegment_AllFlags_SingleSegmentLine()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("only"))))));
        var result = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.True(result.IsLastSegmentOfLine);
        Assert.True(result.IsLastSegmentOfParagraph);
        Assert.True(result.IsLastSegmentOfPage);
        Assert.True(result.IsLastSegmentOfScript);
    }

    [Fact]
    public void RenderNextLineSegment_AllFlags_FullSequence()
    {
        var nav = CreateNav(Block(
            Pg(
                Para(MultiSeg(Seg(T("p0p0s0")), Seg(T("p0p0s1")))),
                Para(Li(T("p0p1s0")))
            ),
            Pg(Para(Li(T("p1p0s0"))))
        ));

        var results = new List<SegmentRenderResult>();
        while (nav.RenderNextLineSegment(RenderSegment) is { } seg)
            results.Add(seg);

        Assert.Equal(4, results.Count);

        Assert.False(results[0].IsLastSegmentOfLine);
        Assert.False(results[0].IsLastSegmentOfPage);

        Assert.True(results[1].IsLastSegmentOfLine);
        Assert.False(results[1].IsLastSegmentOfPage);

        Assert.True(results[2].IsLastSegmentOfLine);
        Assert.True(results[2].IsLastSegmentOfPage);
        Assert.False(results[2].IsLastPage);

        Assert.True(results[3].IsLastSegmentOfLine);
        Assert.True(results[3].IsLastSegmentOfScript);
        Assert.True(results[3].IsLastPage);
    }

    // ─── RenderNextLineSegment: Pointer ─────────────────────────────────────

    [Fact]
    public void RenderNextLineSegment_Pointer_IsLineLevel()
    {
        var nav = CreateNav(Block(Pg(Para(MultiSeg(Seg(T("A")), Seg(T("B")))))));
        var r1 = nav.RenderNextLineSegment(RenderSegment)!;
        var r2 = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.Equal(new ScriptPointer(0, 0, 0), r1.Pointer);
        Assert.Equal(new ScriptPointer(0, 0, 0), r2.Pointer);
    }

    [Fact]
    public void RenderNextLineSegment_Pointer_AdvancesOnlyWhenLineChanges()
    {
        var nav = CreateNav(Block(Pg(Para(
            MultiSeg(Seg(T("A")), Seg(T("B"))),
            Li(T("C"))
        ))));
        nav.RenderNextLineSegment(RenderSegment);
        nav.RenderNextLineSegment(RenderSegment);
        var result = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.Equal(new ScriptPointer(0, 0, 1), result.Pointer);
    }

    // ─── RenderNextLineSegment + RenderNextLine Interaction ─────────────────

    [Fact]
    public void RenderNextLineSegment_ThenRenderNextLine_ConcatenatesRemaining()
    {
        var nav = CreateNav(Block(Pg(Para(
            MultiSeg(Seg(T("Hello, ")), Seg(T("World!"))),
            Li(T("Goodbye"))
        ))));
        nav.RenderNextLineSegment(RenderSegment);
        var result = nav.RenderNextLine(RenderLine)!;
        Assert.Equal("Hello, World!", result.Text);
    }

    [Fact]
    public void RenderNextLine_ThenRenderNextLineSegment_NextLine()
    {
        var nav = CreateNav(Block(Pg(Para(
            Li(T("First")),
            MultiSeg(Seg(T("A")), Seg(T("B")))
        ))));
        nav.RenderNextLine(RenderLine);
        var result = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.Equal("A", result.Text);
    }

    // ─── JumpTo/Reset with Segments ─────────────────────────────────────────

    [Fact]
    public void JumpTo_ThenRenderNextLineSegment_StartsAtCorrectSegment()
    {
        var nav = CreateNav(Block(Pg(Para(
            MultiSeg(Seg(T("A")), Seg(T("B"))),
            MultiSeg(Seg(T("C")), Seg(T("D")))
        ))));
        nav.JumpTo(new ScriptPointer(0, 0, 1));
        var result = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.Equal("C", result.Text);
    }

    [Fact]
    public void Reset_ResetsSegmentIndex()
    {
        var nav = CreateNav(Block(Pg(Para(MultiSeg(Seg(T("A")), Seg(T("B")))))));
        nav.RenderNextLineSegment(RenderSegment);
        nav.RenderNextLineSegment(RenderSegment);
        nav.Reset();
        var result = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.Equal("A", result.Text);
    }

    // ─── RenderNextLineSegment: Async ───────────────────────────────────────

    [Fact]
    public async Task RenderNextLineSegmentAsync_EmptyScript_ReturnsNull()
    {
        var nav = CreateNav(Block());
        Assert.Null(await nav.RenderNextLineSegmentAsync(RenderSegmentAsync));
    }

    [Fact]
    public async Task RenderNextLineSegmentAsync_MatchesSync()
    {
        var syncNav = CreateNav(Block(Pg(Para(
            MultiSeg(Seg(T("A")), Seg(T("B"))),
            Li(T("C"))
        ))));
        var asyncNav = CreateNav(Block(Pg(Para(
            MultiSeg(Seg(T("A")), Seg(T("B"))),
            Li(T("C"))
        ))));

        var syncResults = new List<string>();
        while (syncNav.RenderNextLineSegment(RenderSegment) is { } seg)
            syncResults.Add(seg.Text);

        var asyncResults = new List<string>();
        while (await asyncNav.RenderNextLineSegmentAsync(RenderSegmentAsync) is { } seg)
            asyncResults.Add(seg.Text);

        Assert.Equal(syncResults, asyncResults);
    }

    [Fact]
    public async Task RenderNextLineSegmentAsync_FlagsMatchSync()
    {
        var syncNav = CreateNav(Block(Pg(Para(
            MultiSeg(Seg(T("A")), Seg(T("B"))),
            Li(T("C"))
        ))));
        var asyncNav = CreateNav(Block(Pg(Para(
            MultiSeg(Seg(T("A")), Seg(T("B"))),
            Li(T("C"))
        ))));

        while (true)
        {
            var syncResult = syncNav.RenderNextLineSegment(RenderSegment);
            var asyncResult = await asyncNav.RenderNextLineSegmentAsync(RenderSegmentAsync);

            if (syncResult is null)
            {
                Assert.Null(asyncResult);
                break;
            }

            Assert.Equal(syncResult.IsLastSegmentOfLine, asyncResult!.IsLastSegmentOfLine);
            Assert.Equal(syncResult.IsLastSegmentOfParagraph, asyncResult.IsLastSegmentOfParagraph);
            Assert.Equal(syncResult.IsLastPage, asyncResult.IsLastPage);
            Assert.Equal(syncResult.Pointer, asyncResult.Pointer);
        }
    }

    // ─── Integration: Mixed Segment Lines ───────────────────────────────────

    [Fact]
    public void FullSequence_MixedSegmentLines_RendersCorrectly()
    {
        var nav = CreateNav(Block(Pg(Para(
            MultiSeg(Seg(T("Hello, ")), Seg(T("World!"))),
            Li(T("Goodbye")),
            MultiSeg(Seg(T("A")), Seg(T("B")), Seg(T("C")))
        ))));

        var results = new List<string>();
        while (nav.RenderNextLineSegment(RenderSegment) is { } seg)
            results.Add(seg.Text);

        Assert.Equal(6, results.Count);
        Assert.Equal("Hello, ", results[0]);
        Assert.Equal("World!", results[1]);
        Assert.Equal("Goodbye", results[2]);
        Assert.Equal("A", results[3]);
        Assert.Equal("B", results[4]);
        Assert.Equal("C", results[5]);
    }

    [Fact]
    public void RenderNextLineSegment_SegmentWithOnlyConsumerCall()
    {
        var call = new ConsumerCallItem("get_name", new List<ArgumentValue>());
        var nav = CreateNav(Block(Pg(Para(MultiSeg(Seg(T("Hi ")), Seg(call))))));

        var r1 = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.Equal("Hi ", r1.Text);

        var r2 = nav.RenderNextLineSegment(RenderSegment)!;
        Assert.Equal("[get_name]", r2.Text);
    }

    [Fact]
    public void RenderNextLineSegment_MixedWithRenderNextLine()
    {
        var nav = CreateNav(Block(Pg(Para(
            MultiSeg(Seg(T("A")), Seg(T("B"))),
            MultiSeg(Seg(T("C")), Seg(T("D")))
        ))));

        Assert.Equal("A", nav.RenderNextLineSegment(RenderSegment)!.Text);
        var fullLine = nav.RenderNextLine(RenderLine)!;
        Assert.Equal("AB", fullLine.Text);
        Assert.Equal("C", nav.RenderNextLineSegment(RenderSegment)!.Text);
    }

    [Fact]
    public void FullSequence_RenderAllSegments_FlagsAreCorrect()
    {
        var nav = CreateNav(Block(
            Pg(
                Para(MultiSeg(Seg(T("s0")), Seg(T("s1")))),
                Para(Li(T("s2")))
            ),
            Pg(Para(Li(T("s3"))))
        ));

        var results = new List<SegmentRenderResult>();
        while (nav.RenderNextLineSegment(RenderSegment) is { } seg)
            results.Add(seg);

        Assert.Equal(4, results.Count);

        Assert.False(results[0].IsLastSegmentOfLine);
        Assert.False(results[0].IsLastPage);

        Assert.True(results[1].IsLastSegmentOfLine);
        Assert.False(results[1].IsLastSegmentOfPage);
        Assert.False(results[1].IsLastPage);

        Assert.True(results[2].IsLastSegmentOfLine);
        Assert.True(results[2].IsLastSegmentOfPage);
        Assert.False(results[2].IsLastPage);

        Assert.True(results[3].IsLastSegmentOfLine);
        Assert.True(results[3].IsLastSegmentOfScript);
        Assert.True(results[3].IsLastPage);
    }
}
