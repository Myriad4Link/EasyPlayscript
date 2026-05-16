using Microsoft.CodeAnalysis;

namespace EasyPlayscript;

/// <summary>
/// Diagnostic descriptors for playscript parsing.
/// </summary>
internal static class PlayscriptDiagnostics
{
    private const string Category = "Playscript";

    internal static readonly DiagnosticDescriptor UnexpectedToken = new DiagnosticDescriptor(
        id: "SCPT002",
        title: "Unexpected token",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor MismatchedInput = new DiagnosticDescriptor(
        id: "SCPT003",
        title: "Mismatched input",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
