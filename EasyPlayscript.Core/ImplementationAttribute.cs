using System;
using JetBrains.Annotations;

namespace EasyPlayscript;

public enum ActionScope
{
    GlobalService,
    TransientNode
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[UsedImplicitly]
public sealed class ImplementationAttribute(string? alias = null) : Attribute
{
    [UsedImplicitly] public string? Alias { get; } = alias;
    [UsedImplicitly] public ActionScope Scope { get; set; } = ActionScope.GlobalService;
}