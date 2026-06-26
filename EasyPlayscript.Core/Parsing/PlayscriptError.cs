namespace EasyPlayscript.Parsing;

/// <summary>
///     Represents a syntax or lexer error in playscript.
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