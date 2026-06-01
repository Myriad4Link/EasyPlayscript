using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;

namespace EasyPlayscript.Parsing;

/// <summary>
/// Helper for Pass 1 structure recognition. Parses playscript source and extracts
/// compiler calls with their associated script block raw content.
/// </summary>
public static class PlayscriptStructureHelper
{
    public static List<StructureResult> ParseStructure(string content)
    {
        var (results, _) = ParseStructureWithErrors(content);
        return results;
    }

    public static (List<StructureResult> results, List<PlayscriptError> errors)
        ParseStructureWithErrors(string content)
    {
        var inputStream = new AntlrInputStream(content);
        var lexer = new PlayscriptStructureLexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new PlayscriptStructureParser(tokens);

        var errors = new List<PlayscriptError>();
        lexer.RemoveErrorListeners();
        parser.RemoveErrorListeners();
        lexer.AddErrorListener(new CollectingErrorListener(errors, isLexer: true));
        parser.AddErrorListener(new CollectingErrorListener(errors, isLexer: false));

        var tree = parser.playscript();
        var visitor = new StructureVisitor();
        visitor.Visit(tree);

        return (visitor.Results, errors);
    }

    private class StructureVisitor : PlayscriptStructureParserBaseVisitor<string>
    {
        public List<StructureResult> Results { get; } = [];

        public override string VisitStatement(PlayscriptStructureParser.StatementContext context)
        {
            var compilerCall = context.compilerCall();
            var identifier = compilerCall.IDENTIFIER().GetText();
            var stringLiteral = compilerCall.STRING_LITERAL().Symbol;
            var cleanArg = stringLiteral.Text.Trim('"');
            var line = stringLiteral.Line;
            var col = stringLiteral.Column;

            string rawContent = null;
            if (context.scriptBlock() != null) rawContent = context.scriptBlock().RAW_CONTENT().GetText();

            Results.Add(new StructureResult(identifier, cleanArg, rawContent, line, col));
            return string.Empty;
        }
    }

    private class CollectingErrorListener(List<PlayscriptError> errors, bool isLexer)
        : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e) =>
            errors.Add(new PlayscriptError(line, charPositionInLine, msg, isLexer));

        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e) =>
            errors.Add(new PlayscriptError(line, charPositionInLine, msg, isLexer));
    }
}