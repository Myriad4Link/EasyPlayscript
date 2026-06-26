using System;

namespace EasyPlayscript;

public enum ActionScope
{
    GlobalService,
    TransientNode
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ImplementationAttribute : Attribute
{
    public string? Alias { get; }
    public ActionScope Scope { get; set; } = ActionScope.GlobalService;

    public ImplementationAttribute(string? alias = null) => Alias = alias;
}
