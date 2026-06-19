using System.Collections.Generic;

namespace EasyPlayscript.Parsing;

public enum InterfaceType { String, Int, Decimal, Bool, Void }

public readonly struct InterfaceParameter
{
    public string Name { get; }
    public InterfaceType Type { get; }

    public InterfaceParameter(string name, InterfaceType type)
    {
        Name = name;
        Type = type;
    }
}

public class InterfaceDeclaration
{
    public string Name { get; }
    public List<InterfaceParameter> Parameters { get; }
    public InterfaceType ReturnType { get; }
    public int Line { get; }
    public int Col { get; }
    public string FilePath { get; set; } = string.Empty;

    public InterfaceDeclaration(string name, List<InterfaceParameter> parameters,
        InterfaceType returnType, int line, int col)
    {
        Name = name;
        Parameters = parameters;
        ReturnType = returnType;
        Line = line;
        Col = col;
    }
}
