using System.Linq;
using System.Threading;
using EasyPlayscript.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EasyPlayscript;

internal static class ImplementationScanner
{
    /// <summary>
    /// Extracts an <see cref="ImplementationInfo"/> from a method decorated with
    /// [Implementation]. Called by <c>ForAttributeWithMetadataName</c> in the generator;
    /// the <see cref="GeneratorAttributeSyntaxContext"/> guarantees the target is a
    /// <see cref="MethodDeclarationSyntax"/> with the attribute present.
    /// </summary>
    /// <param name="ctx">
    /// The syntax context provided by <c>ForAttributeWithMetadataName</c>.
    /// <see cref="GeneratorAttributeSyntaxContext.TargetNode"/> is a <see cref="MethodDeclarationSyntax"/>,
    /// <see cref="GeneratorAttributeSyntaxContext.TargetSymbol"/> is an <see cref="IMethodSymbol"/>,
    /// and <see cref="GeneratorAttributeSyntaxContext.Attributes"/> contains the matched attribute.
    /// </param>
    /// <param name="ct">Cancellation token for the generator transform phase.</param>
    /// <returns>
    /// An <see cref="ImplementationInfo"/> populated with the method's containing type, name,
    /// alias (from the attribute constructor argument), parameter types, return type, and source location.
    /// </returns>
    public static ImplementationInfo Extract(
        GeneratorAttributeSyntaxContext ctx,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var methodDecl = (MethodDeclarationSyntax)ctx.TargetNode;
        var methodSymbol = (IMethodSymbol)ctx.TargetSymbol;
        var attr = ctx.Attributes[0];

        var containingType = methodSymbol.ContainingType;

        string? alias = null;
        if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string { Length: > 0 } s)
            alias = s;

        var paramTypes = methodSymbol.Parameters
            .Select(p => MapToCSharpTypeName(p.Type))
            .ToList();

        var returnType = MapToCSharpTypeName(methodSymbol.ReturnType);

        var location = methodDecl.GetLocation();
        var lineSpan = location.GetLineSpan();

        return new ImplementationInfo
        {
            ClassName = FormatFullyQualifiedTypeName(containingType),
            MethodName = methodSymbol.Name,
            Alias = alias,
            ParameterTypeNames = paramTypes,
            ReturnTypeName = returnType,
            FilePath = lineSpan.Path,
            Line = lineSpan.StartLinePosition.Line + 1
        };
    }

    private static string FormatFullyQualifiedTypeName(INamedTypeSymbol type)
    {
        if (type.ContainingNamespace is { IsGlobalNamespace: false } ns)
            return $"global::{ns}.{type.Name}";
        return type.Name;
    }

    private static string MapToCSharpTypeName(ITypeSymbol type)
    {
        var special = type.SpecialType;
        return special switch
        {
            SpecialType.System_String => "string",
            SpecialType.System_Int32 => "int",
            SpecialType.System_Double => "double",
            SpecialType.System_Boolean => "bool",
            SpecialType.System_Void => "void",
            _ => type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
        };
    }
}