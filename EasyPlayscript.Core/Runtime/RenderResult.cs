namespace EasyPlayscript.Runtime;

public abstract class RenderResult(string text, ScriptPointer pointer, bool isLastPage)
{
    public string Text { get; } = text;
    public ScriptPointer Pointer { get; } = pointer;
    public bool IsLastPage { get; } = isLastPage;
}

public sealed class LineRenderResult(
    string text,
    ScriptPointer pointer,
    bool isLastLineOfParagraph,
    bool isLastParagraphOfPage,
    bool isLastPage,
    bool isLastLineOfPage,
    bool isLastLineOfScript,
    bool isLastParagraphOfScript)
    : RenderResult(text, pointer, isLastPage)
{
    public bool IsLastLineOfParagraph { get; } = isLastLineOfParagraph;
    public bool IsLastParagraphOfPage { get; } = isLastParagraphOfPage;
    public bool IsLastLineOfPage { get; } = isLastLineOfPage;
    public bool IsLastLineOfScript { get; } = isLastLineOfScript;
    public bool IsLastParagraphOfScript { get; } = isLastParagraphOfScript;
}

public sealed class SegmentRenderResult(
    string text,
    ScriptPointer pointer,
    bool isLastPage,
    bool isLastSegmentOfLine,
    bool isLastSegmentOfParagraph,
    bool isLastSegmentOfPage,
    bool isLastSegmentOfScript)
    : RenderResult(text, pointer, isLastPage)
{
    public bool IsLastSegmentOfLine { get; } = isLastSegmentOfLine;
    public bool IsLastSegmentOfParagraph { get; } = isLastSegmentOfParagraph;
    public bool IsLastSegmentOfPage { get; } = isLastSegmentOfPage;
    public bool IsLastSegmentOfScript { get; } = isLastSegmentOfScript;
}

public sealed class ParagraphRenderResult(
    string text,
    ScriptPointer pointer,
    bool isLastParagraphOfPage,
    bool isLastPage,
    bool isLastParagraphOfScript)
    : RenderResult(text, pointer, isLastPage)
{
    public bool IsLastParagraphOfPage { get; } = isLastParagraphOfPage;
    public bool IsLastParagraphOfScript { get; } = isLastParagraphOfScript;
}

public sealed class PageRenderResult(
    string text,
    ScriptPointer pointer,
    bool isLastPage)
    : RenderResult(text, pointer, isLastPage);
