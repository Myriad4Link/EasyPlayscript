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
    private int _segmentIndex;

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
        _segmentIndex = 0;
    }

    public void Reset()
    {
        _pageIndex = 0;
        _paragraphIndex = 0;
        _lineIndex = 0;
        _segmentIndex = 0;
    }

    public bool IsLastLineOfParagraph => Block.Pages.Count == 0 || IsEnd() ||
                                         _lineIndex >= Block.Pages[_pageIndex].Paragraphs[_paragraphIndex].Lines.Count -
                                         1;

    public bool IsLastParagraphOfPage => Block.Pages.Count == 0 || IsEnd() ||
                                         _paragraphIndex >= Block.Pages[_pageIndex].Paragraphs.Count - 1;

    public bool IsLastPage => Block.Pages.Count == 0 || IsEnd() ||
                              _pageIndex >= Block.Pages.Count - 1;

    public bool IsLastLineOfPage => IsLastLineOfParagraph && IsLastParagraphOfPage;
    public bool IsLastLineOfScript => IsLastLineOfPage && IsLastPage;
    public bool IsLastParagraphOfScript => IsLastParagraphOfPage && IsLastPage;

    public bool IsLastSegmentOfLine
    {
        get
        {
            if (Block.Pages.Count == 0 || IsEnd()) return true;
            var line = Block.Pages[_pageIndex].Paragraphs[_paragraphIndex].Lines[_lineIndex];
            return _segmentIndex >= line.Segments.Count - 1;
        }
    }

    public bool IsLastSegmentOfParagraph => IsLastSegmentOfLine && IsLastLineOfParagraph;
    public bool IsLastSegmentOfPage => IsLastSegmentOfParagraph && IsLastParagraphOfPage;
    public bool IsLastSegmentOfScript => IsLastSegmentOfPage && IsLastPage;

    // ── RenderNextLineSegment (sync) ────────────────────────────────────────

    public SegmentRenderResult? RenderNextLineSegment(Func<Segment, string> renderSegment)
    {
        if (IsEnd()) return null;

        var currentLine = Block.Pages[_pageIndex].Paragraphs[_paragraphIndex].Lines[_lineIndex];
        if (_segmentIndex >= currentLine.Segments.Count)
        {
            AdvanceLine();
            if (IsEnd()) return null;
            currentLine = Block.Pages[_pageIndex].Paragraphs[_paragraphIndex].Lines[_lineIndex];
            _segmentIndex = 0;
        }

        var segment = currentLine.Segments[_segmentIndex];
        var pointer = Pointer;
        var isLastSegOfLine = _segmentIndex >= currentLine.Segments.Count - 1;
        var isLastSegOfParagraph = isLastSegOfLine && IsLastLineOfParagraph;
        var isLastSegOfPage = isLastSegOfParagraph && IsLastParagraphOfPage;
        var isLastPageVal = IsLastPage;
        var isLastSegOfScript = isLastSegOfPage && isLastPageVal;

        var result = new SegmentRenderResult(
            renderSegment(segment),
            pointer,
            isLastPageVal,
            isLastSegOfLine,
            isLastSegOfParagraph,
            isLastSegOfPage,
            isLastSegOfScript);

        _segmentIndex++;
        return result;
    }

    // ── RenderNextLineSegment (async) ───────────────────────────────────────

    public async Task<SegmentRenderResult?> RenderNextLineSegmentAsync(Func<Segment, Task<string>> renderSegment)
    {
        if (IsEnd()) return null;

        var currentLine = Block.Pages[_pageIndex].Paragraphs[_paragraphIndex].Lines[_lineIndex];
        if (_segmentIndex >= currentLine.Segments.Count)
        {
            AdvanceLine();
            if (IsEnd()) return null;
            currentLine = Block.Pages[_pageIndex].Paragraphs[_paragraphIndex].Lines[_lineIndex];
            _segmentIndex = 0;
        }

        var segment = currentLine.Segments[_segmentIndex];
        var pointer = Pointer;
        var isLastSegOfLine = _segmentIndex >= currentLine.Segments.Count - 1;
        var isLastSegOfParagraph = isLastSegOfLine && IsLastLineOfParagraph;
        var isLastSegOfPage = isLastSegOfParagraph && IsLastParagraphOfPage;
        var isLastPageVal = IsLastPage;
        var isLastSegOfScript = isLastSegOfPage && isLastPageVal;

        var text = await renderSegment(segment);
        var result = new SegmentRenderResult(
            text,
            pointer,
            isLastPageVal,
            isLastSegOfLine,
            isLastSegOfParagraph,
            isLastSegOfPage,
            isLastSegOfScript);

        _segmentIndex++;
        return result;
    }

    // ── RenderNextLine (sync) ───────────────────────────────────────────────

    public LineRenderResult? RenderNextLine(Func<Line, string> renderLine)
    {
        if (IsEnd()) return null;
        var currentLine = Block.Pages[_pageIndex].Paragraphs[_paragraphIndex].Lines[_lineIndex];
        var pointer = Pointer;
        var result = new LineRenderResult(
            renderLine(currentLine),
            pointer,
            IsLastLineOfParagraph,
            IsLastParagraphOfPage,
            IsLastPage,
            IsLastLineOfPage,
            IsLastLineOfScript,
            IsLastParagraphOfScript);
        AdvanceLine();
        _segmentIndex = 0;
        return result;
    }

    // ── RenderNextParagraph (sync) ──────────────────────────────────────────

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
        _segmentIndex = 0;
        return new ParagraphRenderResult(sb.ToString(), pointer,
            isLastParagraphOfPage, isLastPageVal, isLastParagraphOfScript);
    }

    // ── RenderNextPage (sync) ───────────────────────────────────────────────

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
        _segmentIndex = 0;
        return new PageRenderResult(sb.ToString(), pointer, isLastPageVal);
    }

    // ── RenderNextLine (async) ──────────────────────────────────────────────

    public async Task<LineRenderResult?> RenderNextLineAsync(Func<Line, Task<string>> renderLine)
    {
        if (IsEnd()) return null;
        var currentLine = Block.Pages[_pageIndex].Paragraphs[_paragraphIndex].Lines[_lineIndex];
        var pointer = Pointer;
        var isLastLineOfParagraph = IsLastLineOfParagraph;
        var isLastParagraphOfPage = IsLastParagraphOfPage;
        var isLastPageVal = IsLastPage;
        var isLastLineOfPage = IsLastLineOfPage;
        var isLastLineOfScript = IsLastLineOfScript;
        var isLastParagraphOfScript = IsLastParagraphOfScript;
        var text = await renderLine(currentLine);
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
        _segmentIndex = 0;
        return result;
    }

    // ── RenderNextParagraph (async) ─────────────────────────────────────────

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
        _segmentIndex = 0;
        return new ParagraphRenderResult(sb.ToString(), pointer,
            isLastParagraphOfPage, isLastPageVal, isLastParagraphOfScript);
    }

    // ── RenderNextPage (async) ──────────────────────────────────────────────

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
        _segmentIndex = 0;
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
