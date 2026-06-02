namespace EasyPlayscript.Parsing;

public readonly struct ValidationDiagnostic
{
    public string Code { get; }
    public string Message { get; }
    public string FilePath { get; }
    public int Line { get; }
    public int Col { get; }
    public object[] MessageArgs { get; }

    public ValidationDiagnostic(string code, string message, string filePath, int line, int col, params object[] messageArgs)
    {
        Code = code;
        Message = message;
        FilePath = filePath;
        Line = line;
        Col = col;
        MessageArgs = messageArgs ?? new object[0];
    }
}
