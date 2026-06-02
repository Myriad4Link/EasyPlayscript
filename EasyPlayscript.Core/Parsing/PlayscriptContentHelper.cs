using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;

namespace EasyPlayscript.Parsing;

/// <summary>
/// Creates ANTLR lexer/parser instances from playscript block content (Pass 2)
/// and collects syntax errors. Used to parse the raw content extracted by Pass 1.
/// </summary>
public static class PlayscriptContentHelper
{
    public static (PlayscriptContentParser parser, List<PlayscriptError> errors) Parse(string input)
    {
        var inputStream = new AntlrInputStream(input.Trim());
        var lexer = new PlayscriptContentLexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new PlayscriptContentParser(tokens);

        var errors = new List<PlayscriptError>();
        lexer.RemoveErrorListeners();
        parser.RemoveErrorListeners();
        lexer.AddErrorListener(new CollectingErrorListener(errors, isLexer: true));
        parser.AddErrorListener(new CollectingErrorListener(errors, isLexer: false));

        return (parser, errors);
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
