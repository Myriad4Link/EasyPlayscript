using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;

namespace EasyPlayscript.Parsing;

/// <summary>
/// Creates ANTLR lexer/parser instances from playscript source text and collects syntax errors.
/// </summary>
public static class PlayscriptParserHelper
{
    public static (PlayscriptParser parser, List<string> errors) Parse(string input)
    {
        var inputStream = new AntlrInputStream(input);
        var lexer = new PlayscriptLexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new PlayscriptParser(tokens);

        var errors = new List<string>();
        lexer.RemoveErrorListeners();
        parser.RemoveErrorListeners();
        lexer.AddErrorListener(new CollectingErrorListener(errors));
        parser.AddErrorListener(new CollectingErrorListener(errors));

        return (parser, errors);
    }

    private class CollectingErrorListener(List<string> errors) : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            errors.Add($"Lexer error at {line}:{charPositionInLine} - {msg}");
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            errors.Add($"Parser error at {line}:{charPositionInLine} - {msg}");
        }
    }
}
