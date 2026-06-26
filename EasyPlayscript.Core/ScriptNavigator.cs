using System;
using System.Collections.Generic;
using System.Text;

namespace EasyPlayscript;

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

    public string? RenderNextLine(Func<Line, string> renderLine)
    {
        if (IsEnd()) return null;
        var line = Block.Pages[_pageIndex].Paragraphs[_paragraphIndex].Lines[_lineIndex];
        var result = renderLine(line);
        AdvanceLine();
        return result;
    }

    public string? RenderNextParagraph(Func<Line, string> renderLine)
    {
        if (IsEnd()) return null;
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
        return sb.ToString();
    }

    public string? RenderNextPage(Func<Line, string> renderLine)
    {
        if (IsEnd()) return null;
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
        return sb.ToString();
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