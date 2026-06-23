using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EasyPlayscript.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EasyPlayscript;

internal static class ImplementationScanner
{
    public static ImplementationInfo? Extract(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (ctx.Node is not MethodDeclarationSyntax methodDecl)
            return null;

        var model = ctx.SemanticModel;
        var symbol = model.GetDeclaredSymbol(methodDecl, ct);
        if (symbol is not IMethodSymbol methodSymbol)
            return null;

        var attr = methodSymbol.GetAttributes().FirstOrDefault(
            a => a.AttributeClass?.Name == "ImplementationAttribute" &&
                 a.AttributeClass.ContainingNamespace.ToDisplayString() == "EasyPlayscript");

        if (attr == null)
            return null;

        var containingType = methodSymbol.ContainingType;

        string? alias = null;
        if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string s && s.Length > 0)
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
