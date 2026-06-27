using System;
using JetBrains.Annotations;

namespace EasyPlayscript.Runtime;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[UsedImplicitly]
public sealed class ImplementationAttribute(string? alias = null) : Attribute
{
    [UsedImplicitly] public string? Alias { get; } = alias;
}