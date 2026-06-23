using System;

namespace EasyPlayscript;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ImplementationAttribute : Attribute
{
    public string? Alias { get; }

    public ImplementationAttribute(string? alias = null) => Alias = alias;
}
