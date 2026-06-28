using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace EasyPlayscript.LSP.Parsing;

internal static class TextEditApplier
{
    /// <summary>
    ///     Applies a sequence of incremental <see cref="TextDocumentContentChangeEvent" /> edits to a document string.
    ///     Each change with a <see cref="TextDocumentContentChangeEvent.Range" /> is spliced in at the specified
    ///     character offsets; a rangeless change replaces the entire text. Changes are applied in order, so
    ///     later offsets are relative to the document state after earlier edits.
    /// </summary>
    public static string ApplyChanges(string text, IEnumerable<TextDocumentContentChangeEvent> changes)
    {
        foreach (var change in changes)
        {
            if (change.Range is null)
            {
                text = change.Text;
                continue;
            }

            var startOffset = PositionToOffset(text, change.Range.Start);
            var endOffset = PositionToOffset(text, change.Range.End);

            if (startOffset > endOffset)
                (startOffset, endOffset) = (endOffset, startOffset);

            text = string.Concat(text.AsSpan(0, startOffset), change.Text, text.AsSpan(endOffset));
        }

        return text;
    }

    public static int PositionToOffset(string text, Position pos)
    {
        var line = 0;
        var col = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (line == pos.Line && col == pos.Character)
                return i;

            if (text[i] == '\n')
            {
                line++;
                col = 0;
            }
            else if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                line++;
                col = 0;
            }
            else
            {
                col++;
            }
        }

        if (line == pos.Line && col == pos.Character)
            return text.Length;

        return text.Length;
    }
}