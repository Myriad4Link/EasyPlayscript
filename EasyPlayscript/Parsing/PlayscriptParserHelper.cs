using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;

namespace EasyPlayscript.Parsing;

/// <summary>
/// Creates ANTLR lexer/parser instances from playscript source text and collects syntax errors.
/// </summary>
public static class PlayscriptParserHelper
{
    public static (PlayscriptParser parser, List<(int line, int col, string msg, bool isLexer)> errors) Parse(string input)
    {
        var inputStream = new AntlrInputStream(input);
        var lexer = new PlayscriptLexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new PlayscriptParser(tokens);

        var errors = new List<(int line, int col, string msg, bool isLexer)>();
        lexer.RemoveErrorListeners();
        parser.RemoveErrorListeners();
        lexer.AddErrorListener(new CollectingErrorListener(errors, isLexer: true));
        parser.AddErrorListener(new CollectingErrorListener(errors, isLexer: false));

        return (parser, errors);
    }

    private class CollectingErrorListener : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        private readonly List<(int line, int col, string msg, bool isLexer)> _errors;
        private readonly bool _isLexer;

        public CollectingErrorListener(List<(int line, int col, string msg, bool isLexer)> errors, bool isLexer)
        {
            _errors = errors;
            _isLexer = isLexer;
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            _errors.Add((line, charPositionInLine, msg, _isLexer));
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            _errors.Add((line, charPositionInLine, msg, _isLexer));
        }
    }
}
