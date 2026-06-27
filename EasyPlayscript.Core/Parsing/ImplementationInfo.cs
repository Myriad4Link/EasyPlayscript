using System.Collections.Generic;

namespace EasyPlayscript.Parsing;

public class ImplementationInfo
{
    public string ClassName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public List<string> ParameterTypeNames { get; set; } = new();
    public string ReturnTypeName { get; set; } = "void";
    public bool IsAsync { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int Line { get; set; }

    public string EffectiveName => Alias ?? MethodName;
}