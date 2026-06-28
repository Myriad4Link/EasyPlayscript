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
}