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
    private static Line Li(params LineItem[] items) => new() { Items = new List<LineItem>(items) };
    private static TextItem T(string text) => new(text);

    private static string RenderLine(Line line)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var item in line.Items)
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

        Assert.Equal("Hello", nav.RenderNextLine(RenderLine));
    }

    [Fact]
    public void RenderNextLine_MultipleTextItems_Concatenates()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("A"), T("B"))))));

        Assert.Equal("AB", nav.RenderNextLine(RenderLine));
    }

    [Fact]
    public void RenderNextLine_DispatchesConsumerCall_AppendsResult()
    {
        var call = new ConsumerCallItem("get_name", new List<ArgumentValue>());
        var nav = CreateNav(Block(Pg(Para(Li(T("Hi, "), call, T("!"))))));

        var result = nav.RenderNextLine(RenderLine)!;

        Assert.Contains("[get_name]", result);
        Assert.Equal("Hi, [get_name]!", result);
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

        Assert.Equal("Hello", nav.RenderNextParagraph(RenderLine));
    }

    [Fact]
    public void RenderNextParagraph_MultipleLines_JoinsWithNewline()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("line1")), Li(T("line2")), Li(T("line3"))))));

        var result = nav.RenderNextParagraph(RenderLine)!;

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

        var result = nav.RenderNextParagraph(RenderLine)!;

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

        Assert.Equal("Hello", nav.RenderNextPage(RenderLine));
    }

    [Fact]
    public void RenderNextPage_MultipleParagraphs_JoinsWithBlankLine()
    {
        var nav = CreateNav(Block(Pg(
            Para(Li(T("para1line1")), Li(T("para1line2"))),
            Para(Li(T("para2line1")))
        )));

        var result = nav.RenderNextPage(RenderLine)!;

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

        var result = nav.RenderNextPage(RenderLine)!;

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

        Assert.Equal("p1p0l0", result);
    }

    [Fact]
    public void Reset_ReturnsToStart()
    {
        var nav = CreateMultiLevelNav();

        nav.RenderNextLine(RenderLine);
        nav.RenderNextLine(RenderLine);
        nav.Reset();

        Assert.Equal(new ScriptPointer(0, 0, 0), nav.Pointer);
        Assert.Equal("p0p0l0", nav.RenderNextLine(RenderLine));
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

        var results = new List<string>();
        while (nav.RenderNextLine(RenderLine) is { } line)
            results.Add(line);

        Assert.Equal(4, results.Count);
        Assert.Equal("p0p0l0", results[0]);
        Assert.Equal("p0p0l1", results[1]);
        Assert.Equal("p0p1l0", results[2]);
        Assert.Equal("p1p0l0", results[3]);
    }

    [Fact]
    public void FullSequence_RenderAllParagraphs_ReturnsExpectedContent()
    {
        var nav = CreateMultiLevelNav();

        var results = new List<string>();
        while (nav.RenderNextParagraph(RenderLine) is { } para)
            results.Add(para);

        Assert.Equal(3, results.Count);
        Assert.Contains("p0p0l0", results[0]);
        Assert.Contains("p0p0l1", results[0]);
        Assert.Equal("p0p1l0", results[1]);
        Assert.Equal("p1p0l0", results[2]);
    }

    [Fact]
    public void FullSequence_RenderAllPages_ReturnsExpectedContent()
    {
        var nav = CreateMultiLevelNav();

        var results = new List<string>();
        while (nav.RenderNextPage(RenderLine) is { } page)
            results.Add(page);

        Assert.Equal(2, results.Count);
        Assert.Contains("p0p0l0", results[0]);
        Assert.Contains("p0p1l0", results[0]);
        Assert.Equal("p1p0l0", results[1]);
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
        Assert.Equal("Hello", await nav.RenderNextLineAsync(RenderLineAsync));
    }

    [Fact]
    public async Task RenderNextLineAsync_MultipleItems_Concatenates()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("A"), T("B"))))));
        Assert.Equal("AB", await nav.RenderNextLineAsync(RenderLineAsync));
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
        Assert.Contains("Line1", result);
        Assert.Contains("Line2", result);
    }

    [Fact]
    public async Task RenderNextPageAsync_MultipleParagraphs_SeparatesWithBlankLine()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("P1"))), Para(Li(T("P2"))))));
        var result = await nav.RenderNextPageAsync(RenderLineAsync);
        Assert.Contains("P1", result);
        Assert.Contains("P2", result);
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
            syncResults.Add(line);

        var asyncResults = new List<string>();
        while (await asyncNav.RenderNextLineAsync(RenderLineAsync) is { } line)
            asyncResults.Add(line);

        Assert.Equal(syncResults, asyncResults);
    }

    [Fact]
    public async Task JumpTo_ThenRenderNextLineAsync_StartsAtNewPosition()
    {
        var nav = CreateNav(Block(Pg(Para(Li(T("First")), Li(T("Second"))))));
        nav.JumpTo(new ScriptPointer(0, 0, 1));
        Assert.Equal("Second", await nav.RenderNextLineAsync(RenderLineAsync));
    }
}
