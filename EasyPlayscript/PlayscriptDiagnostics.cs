using Microsoft.CodeAnalysis;

namespace EasyPlayscript;

/// <summary>
/// Diagnostic descriptors for playscript parsing and semantic validation.
/// </summary>
internal static class PlayscriptDiagnostics
{
    private const string Category = "Playscript";

    internal static readonly DiagnosticDescriptor OrphanedScriptBlock = new DiagnosticDescriptor(
        id: "SCPT001",
        title: "Orphaned script block",
        messageFormat: "Script block must follow an external call (e.g. @script(\"name\")[...])",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

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
