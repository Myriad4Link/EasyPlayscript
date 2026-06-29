using System;
using System.Text;
using System.Threading.Tasks;
using EasyPlayscript.DataModel;

namespace EasyPlayscript.Runtime;

public class ScriptNavigator(ScriptBlock block)
{
    private ScriptBlock Block { get; } = block ?? throw new ArgumentNullException(nameof(block));

    private int _pageIndex;
    private int _paragraphIndex;
    private int _lineIndex;

    public ScriptPointer Pointer => new(_pageIndex, _paragraphIndex, _lineIndex);

    public void JumpTo(ScriptPointer pointer)
    {
        if (pointer.PageIndex < 0 || pointer.PageIndex >= Block.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pointer), "PageIndex out of range.");
        if (pointer.ParagraphIndex < 0 || pointer.ParagraphIndex >= Block.Pages[pointer.PageIndex].Paragraphs.Count)
            throw new ArgumentOutOfRangeException(nameof(pointer), "ParagraphIndex out of range.");
        if (pointer.LineIndex < 0 || pointer.LineIndex >=
            Block.Pages[pointer.PageIndex].Paragraphs[pointer.ParagraphIndex].Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(pointer), "LineIndex out of range.");
        _pageIndex = pointer.PageIndex;
        _paragraphIndex = pointer.ParagraphIndex;
        _lineIndex = pointer.LineIndex;
    }

    public void Reset()
    {
        _pageIndex = 0;
        _paragraphIndex = 0;
        _lineIndex = 0;
    }

    /// <summary>
    ///     True if the current line is the last in its paragraph, or if the script is empty / the pointer is past the end.
    ///     Returns <c>true</c> for empty scripts and when <see cref="IsEnd"/> is true.
    /// </summary>
    public bool IsLastLineOfParagraph => Block.Pages.Count == 0 || IsEnd() ||
                                         _lineIndex >= Block.Pages[_pageIndex].Paragraphs[_paragraphIndex].Lines.Count -
                                         1;

    /// <summary>
    ///     True if the current paragraph is the last on its page, or if the script is empty / the pointer is past the end.
    ///     Returns <c>true</c> for empty scripts and when <see cref="IsEnd"/> is true.
    /// </summary>
    public bool IsLastParagraphOfPage => Block.Pages.Count == 0 || IsEnd() ||
                                         _paragraphIndex >= Block.Pages[_pageIndex].Paragraphs.Count - 1;

    /// <summary>
    ///     True if the current page is the last in the script, or if the script is empty / the pointer is past the end.
    ///     Returns <c>true</c> for empty scripts and when <see cref="IsEnd"/> is true.
    /// </summary>
    public bool IsLastPage => Block.Pages.Count == 0 || IsEnd() ||
                              _pageIndex >= Block.Pages.Count - 1;

    /// <summary>True if the current line is the last on its page (i.e., last line of last paragraph on the page).</summary>
    public bool IsLastLineOfPage => IsLastLineOfParagraph && IsLastParagraphOfPage;
    /// <summary>True if the current line is the last in the entire script.</summary>
    public bool IsLastLineOfScript => IsLastLineOfPage && IsLastPage;
    /// <summary>True if the current paragraph is the last in the entire script.</summary>
    public bool IsLastParagraphOfScript => IsLastParagraphOfPage && IsLastPage;

    public LineRenderResult? RenderNextLine(Func<Line, string> renderLine)
    {
        if (IsEnd()) return null;
        var pointer = Pointer;
        var result = new LineRenderResult(
            renderLine(Block.Pages[_pageIndex].Paragraphs[_paragraphIndex].Lines[_lineIndex]),
            pointer,
            IsLastLineOfParagraph,
            IsLastParagraphOfPage,
            IsLastPage,
            IsLastLineOfPage,
            IsLastLineOfScript,
            IsLastParagraphOfScript);
        AdvanceLine();
        return result;
    }

    public ParagraphRenderResult? RenderNextParagraph(Func<Line, string> renderLine)
    {
        if (IsEnd()) return null;
        var pointer = Pointer;
        var isLastParagraphOfPage = IsLastParagraphOfPage;
        var isLastParagraphOfScript = IsLastParagraphOfScript;
        var isLastPageVal = IsLastPage;

        var sb = new StringBuilder();
        var paragraph = Block.Pages[_pageIndex].Paragraphs[_paragraphIndex];
        for (var i = 0; i < paragraph.Lines.Count; i++)
        {
            if (i > 0) sb.AppendLine();
            sb.Append(renderLine(paragraph.Lines[i]));
        }

        _paragraphIndex++;
        if (_paragraphIndex >= Block.Pages[_pageIndex].Paragraphs.Count)
        {
            _paragraphIndex = 0;
            _pageIndex++;
        }

        _lineIndex = 0;
        return new ParagraphRenderResult(sb.ToString(), pointer,
            isLastParagraphOfPage, isLastPageVal, isLastParagraphOfScript);
    }

    public PageRenderResult? RenderNextPage(Func<Line, string> renderLine)
    {
        if (IsEnd()) return null;
        var pointer = Pointer;
        var isLastPageVal = IsLastPage;

        var sb = new StringBuilder();
        var page = Block.Pages[_pageIndex];
        for (var pi = 0; pi < page.Paragraphs.Count; pi++)
        {
            if (pi > 0)
            {
                sb.AppendLine();
                sb.AppendLine();
            }

            var paragraph = page.Paragraphs[pi];
            for (var li = 0; li < paragraph.Lines.Count; li++)
            {
                if (li > 0) sb.AppendLine();
                sb.Append(renderLine(paragraph.Lines[li]));
            }
        }

        _pageIndex++;
        _paragraphIndex = 0;
        _lineIndex = 0;
        return new PageRenderResult(sb.ToString(), pointer, isLastPageVal);
    }

    public async Task<LineRenderResult?> RenderNextLineAsync(Func<Line, Task<string>> renderLine)
    {
        if (IsEnd()) return null;
        var pointer = Pointer;
        var isLastLineOfParagraph = IsLastLineOfParagraph;
        var isLastParagraphOfPage = IsLastParagraphOfPage;
        var isLastPageVal = IsLastPage;
        var isLastLineOfPage = IsLastLineOfPage;
        var isLastLineOfScript = IsLastLineOfScript;
        var isLastParagraphOfScript = IsLastParagraphOfScript;
        var text = await renderLine(Block.Pages[_pageIndex].Paragraphs[_paragraphIndex].Lines[_lineIndex]);
        var result = new LineRenderResult(
            text,
            pointer,
            isLastLineOfParagraph,
            isLastParagraphOfPage,
            isLastPageVal,
            isLastLineOfPage,
            isLastLineOfScript,
            isLastParagraphOfScript);
        AdvanceLine();
        return result;
    }

    public async Task<ParagraphRenderResult?> RenderNextParagraphAsync(Func<Line, Task<string>> renderLine)
    {
        if (IsEnd()) return null;
        var pointer = Pointer;
        var isLastParagraphOfPage = IsLastParagraphOfPage;
        var isLastParagraphOfScript = IsLastParagraphOfScript;
        var isLastPageVal = IsLastPage;

        var sb = new StringBuilder();
        var paragraph = Block.Pages[_pageIndex].Paragraphs[_paragraphIndex];
        for (var i = 0; i < paragraph.Lines.Count; i++)
        {
            if (i > 0) sb.AppendLine();
            sb.Append(await renderLine(paragraph.Lines[i]));
        }

        _paragraphIndex++;
        if (_paragraphIndex >= Block.Pages[_pageIndex].Paragraphs.Count)
        {
            _paragraphIndex = 0;
            _pageIndex++;
        }

        _lineIndex = 0;
        return new ParagraphRenderResult(sb.ToString(), pointer,
            isLastParagraphOfPage, isLastPageVal, isLastParagraphOfScript);
    }

    public async Task<PageRenderResult?> RenderNextPageAsync(Func<Line, Task<string>> renderLine)
    {
        if (IsEnd()) return null;
        var pointer = Pointer;
        var isLastPageVal = IsLastPage;

        var sb = new StringBuilder();
        var page = Block.Pages[_pageIndex];
        for (var pi = 0; pi < page.Paragraphs.Count; pi++)
        {
            if (pi > 0)
            {
                sb.AppendLine();
                sb.AppendLine();
            }

            var paragraph = page.Paragraphs[pi];
            for (var li = 0; li < paragraph.Lines.Count; li++)
            {
                if (li > 0) sb.AppendLine();
                sb.Append(await renderLine(paragraph.Lines[li]));
            }
        }

        _pageIndex++;
        _paragraphIndex = 0;
        _lineIndex = 0;
        return new PageRenderResult(sb.ToString(), pointer, isLastPageVal);
    }

    private bool IsEnd() => _pageIndex >= Block.Pages.Count;

    private void AdvanceLine()
    {
        _lineIndex++;
        if (_pageIndex >= Block.Pages.Count) return;
        var currentPage = Block.Pages[_pageIndex];
        if (_paragraphIndex >= currentPage.Paragraphs.Count) return;
        var currentParagraph = currentPage.Paragraphs[_paragraphIndex];
        if (_lineIndex < currentParagraph.Lines.Count) return;
        _lineIndex = 0;
        _paragraphIndex++;
        if (_paragraphIndex < currentPage.Paragraphs.Count) return;
        _paragraphIndex = 0;
        _pageIndex++;
    }
}
