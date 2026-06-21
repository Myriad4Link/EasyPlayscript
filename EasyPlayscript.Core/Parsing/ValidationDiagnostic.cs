namespace EasyPlayscript.Parsing;

public readonly struct ValidationDiagnostic(
    string code,
    string message,
    string filePath,
    int line,
    int col,
    params object[] messageArgs)
{
    public string Code { get; } = code;
    public string Message { get; } = message;
    public string FilePath { get; } = filePath;
    public int Line { get; } = line;
    public int Col { get; } = col;
    public object[] MessageArgs { get; } = messageArgs ?? [];
}
