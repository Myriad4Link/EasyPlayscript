using System;

namespace EasyPlayscript.Runtime;

public readonly struct ScriptPointer(int pageIndex, int paragraphIndex, int lineIndex)
    : IEquatable<ScriptPointer>
{
    public int PageIndex { get; } = pageIndex;
    public int ParagraphIndex { get; } = paragraphIndex;
    public int LineIndex { get; } = lineIndex;

    public bool Equals(ScriptPointer other) =>
        PageIndex == other.PageIndex &&
        ParagraphIndex == other.ParagraphIndex &&
        LineIndex == other.LineIndex;

    public override bool Equals(object? obj) =>
        obj is ScriptPointer other && Equals(other);

    public override int GetHashCode() =>
        (PageIndex, ParagraphIndex, LineIndex).GetHashCode();

    public override string ToString() =>
        $"({PageIndex}, {ParagraphIndex}, {LineIndex})";

    public static bool operator ==(ScriptPointer left, ScriptPointer right) => left.Equals(right);
    public static bool operator !=(ScriptPointer left, ScriptPointer right) => !left.Equals(right);
}
