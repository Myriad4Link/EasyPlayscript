namespace EasyPlayscript.Parsing;

public readonly struct ValidationDiagnostic(
    string code,
    string format,
    string filePath,
    int line,
    int col,
    params object[] messageArgs)
{
    public string Code { get; } = code;
    public string Format { get; } = format;
    public string FilePath { get; } = filePath;
    public int Line { get; } = line;
    public int Col { get; } = col;
    public object[] MessageArgs { get; } = messageArgs ?? [];

    public string Message =>
        MessageArgs.Length > 0
            ? string.Format(Format, MessageArgs)
            : Format;

    public static ValidationDiagnostic CreateRaw(
        string code, string message, string filePath, int line, int col)
    {
        return new ValidationDiagnostic(code, message, filePath, line, col);
    }
}