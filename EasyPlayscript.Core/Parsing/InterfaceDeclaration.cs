using System.Collections.Generic;

namespace EasyPlayscript.Parsing;

public enum InterfaceType
{
    String,
    Int,
    Decimal,
    Bool,
    Void
}

public readonly struct InterfaceParameter(string name, InterfaceType type)
{
    public string Name { get; } = name;
    public InterfaceType Type { get; } = type;
}

public class InterfaceDeclaration(
    string name,
    List<InterfaceParameter> parameters,
    InterfaceType returnType,
    int line,
    int col)
{
    public string Name { get; } = name;
    public List<InterfaceParameter> Parameters { get; } = parameters;
    public InterfaceType ReturnType { get; } = returnType;
    public int Line { get; } = line;
    public int Col { get; } = col;
    public string FilePath { get; set; } = string.Empty;
}