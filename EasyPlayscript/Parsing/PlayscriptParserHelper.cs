using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;

namespace EasyPlayscript.Parsing;

/// <summary>
/// Represents a syntax or lexer error in playscript.
/// </summary>
public readonly struct PlayscriptError(int line, int col, string msg, bool isLexer)
{
    public int Line { get; } = line;
    public int Col { get; } = col;
    public string Msg { get; } = msg;
    public bool IsLexer { get; } = isLexer;

    public void Deconstruct(out int line, out int col, out string msg, out bool isLexer)
    {
        line = Line;
        col = Col;
        msg = Msg;
        isLexer = IsLexer;
    }
}

/// <summary>
/// Creates ANTLR lexer/parser instances from playscript source text and collects syntax errors.
/// </summary>
public static class PlayscriptParserHelper
{
    public static (PlayscriptParser parser, List<PlayscriptError> errors) Parse(string input)
    {
        var inputStream = new AntlrInputStream(input);
        var lexer = new PlayscriptLexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new PlayscriptParser(tokens);

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
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            errors.Add(new PlayscriptError(line, charPositionInLine, msg, isLexer));
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            errors.Add(new PlayscriptError(line, charPositionInLine, msg, isLexer));
        }
    }
}
