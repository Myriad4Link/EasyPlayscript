using EasyPlayscript.Parsing;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace EasyPlayscript.LSP.Mapping;

internal static class PositionMapper
{
    public static int ToLspLine(int antlrLine)
    {
        return antlrLine - 1;
    }

    public static int ToLspCol(int antlrCol)
    {
        return antlrCol;
    }

    public static int ToAbsoluteLine(int contentLine, in BlockOffset block)
    {
        // -1 for ANTLR → LSP, -1 for content → file
        return block.ContentStartLine + contentLine - 2;
    }

    public static Position ToLspPosition(int antlrLine, int antlrCol)
    {
        return new Position(ToLspLine(antlrLine), ToLspCol(antlrCol));
    }

    public static Position ToAbsolutePosition(int contentLine, int contentCol, in BlockOffset block)
    {
        return new Position(ToAbsoluteLine(contentLine, block), contentCol);
    }

    public static Range ToLspRange(int startLine, int startCol, int endLine, int endCol)
    {
        return new Range(ToLspLine(startLine), ToLspCol(startCol), ToLspLine(endLine), ToLspCol(endCol));
    }

    public static Range ToAbsoluteRange(int startLine, int startCol, int endLine, int endCol, in BlockOffset block)
    {
        return new Range(ToAbsoluteLine(startLine, block), startCol, ToAbsoluteLine(endLine, block), endCol);
    }

    public static Diagnostic ToLspDiagnostic(PlayscriptError error, DocumentUri uri)
    {
        var line = ToLspLine(error.Line);
        var col = ToLspCol(error.Col);
        return new Diagnostic
        {
            Range = new Range(new Position(line, col), new Position(line, col + 1)),
            Severity = DiagnosticSeverity.Error,
            Source = "EasyPlayscript",
            Message = error.Msg
        };
    }

    public static Diagnostic ToLspDiagnostic(PlayscriptError error, DocumentUri uri, in BlockOffset block)
    {
        var line = ToAbsoluteLine(error.Line, block);
        var col = error.Col;
        return new Diagnostic
        {
            Range = new Range(new Position(line, col), new Position(line, col + 1)),
            Severity = DiagnosticSeverity.Error,
            Source = "EasyPlayscript",
            Message = error.Msg
        };
    }

    /// <summary>
    ///     Converts a Core <see cref="ValidationDiagnostic" /> (1-based line, 0-based col)
    ///     into an LSP <see cref="Diagnostic" />. The column is clamped to the end of the
    ///     target line so that synthetic positions (col = 0 used by some validators) and
    ///     out-of-range columns still produce a valid squiggle. Severity is mapped from
    ///     the diagnostic code: SCPT011 (unused implementation) becomes a warning; the
    ///     rest are errors.
    /// </summary>
    /// <param name="diag">The Core validation diagnostic to convert.</param>
    /// <param name="fileText">
    ///     The full document text, used to look up the target line's length for column
    ///     clamping. <c>null</c> skips clamping (used when text is not yet known).
    /// </param>
    public static Diagnostic ToLspDiagnostic(ValidationDiagnostic diag, string? fileText)
    {
        var line = ToLspLine(diag.Line);
        var lineLength = GetLineLength(fileText, diag.Line);

        // Three cases:
        //  - line in range: clamp col to [0, lineLength].
        //  - line past EOF in a known file: column is meaningless → 0.
        //  - no file text at all: honor the validator's reported col (clamped to >= 0).
        int col;
        int endCol;
        if (lineLength is { } len)
        {
            col = Math.Clamp(diag.Col, 0, len);
            endCol = Math.Min(len, col + 1);
        }
        else if (fileText is null)
        {
            col = Math.Max(0, diag.Col);
            endCol = col + 1;
        }
        else
        {
            col = 0;
            endCol = 1;
        }

        return new Diagnostic
        {
            Range = new Range(new Position(line, col), new Position(line, endCol)),
            Severity = diag.Code == DiagnosticCodes.UnusedImplementation
                ? DiagnosticSeverity.Warning
                : DiagnosticSeverity.Error,
            Source = "EasyPlayscript",
            Code = diag.Code,
            Message = diag.Message
        };
    }

    private static int? GetLineLength(string? text, int oneBasedLine)
    {
        if (text is null) return null;
        if (oneBasedLine < 1) return null;

        var current = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (current == oneBasedLine)
            {
                var lineEnd = text.IndexOf('\n', i);
                if (lineEnd < 0) lineEnd = text.Length;
                var lineEndNoCr = lineEnd;
                if (lineEndNoCr > i && text[lineEndNoCr - 1] == '\r') lineEndNoCr--;
                return lineEndNoCr - i;
            }

            if (text[i] == '\n') current++;
        }

        if (current == oneBasedLine) return 0;
        return null;
    }
}
