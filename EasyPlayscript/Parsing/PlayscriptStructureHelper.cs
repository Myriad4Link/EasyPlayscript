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
    public static List<(string identifier, string name, string rawContent, int line, int col)>
        ParseStructure(string content)
    {
        var (results, _) = ParseStructureWithErrors(content);
        return results;
    }

    public static (List<(string identifier, string name, string rawContent, int line, int col)> results,
                    List<PlayscriptError> errors)
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
        public List<(string identifier, string name, string rawContent, int line, int col)> Results { get; } = new();

        public override string VisitStatement(PlayscriptStructureParser.StatementContext context)
        {
            var compilerCall = context.compilerCall();
            var identifier = compilerCall.IDENTIFIER().GetText();
            var stringLiteral = compilerCall.STRING_LITERAL().Symbol;
            var cleanArg = stringLiteral.Text.Trim('"');
            var line = stringLiteral.Line;
            var col = stringLiteral.Column;

            string rawContent = null;
            if (context.scriptBlock() != null)
            {
                rawContent = context.scriptBlock().RAW_CONTENT().GetText();
            }

            Results.Add((identifier, cleanArg, rawContent, line, col));
            return string.Empty;
        }
    }

    private class CollectingErrorListener : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        private readonly List<PlayscriptError> _errors;
        private readonly bool _isLexer;

        public CollectingErrorListener(List<PlayscriptError> errors, bool isLexer)
        {
            _errors = errors;
            _isLexer = isLexer;
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            _errors.Add(new PlayscriptError(line, charPositionInLine, msg, _isLexer));
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            _errors.Add(new PlayscriptError(line, charPositionInLine, msg, _isLexer));
        }
    }
}
