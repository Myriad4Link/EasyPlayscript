using Microsoft.CodeAnalysis;

namespace EasyPlayscript;

/// <summary>
/// Diagnostic descriptors for playscript parsing.
/// </summary>
internal static class PlayscriptDiagnostics
{
    private const string Category = "Playscript";

    internal static readonly DiagnosticDescriptor UnexpectedToken = new(
        id: "SCPT002",
        title: "Unexpected token",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor MismatchedInput = new(
        id: "SCPT003",
        title: "Mismatched input",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor DuplicateScriptName = new(
        id: "SCPT004",
        title: "Duplicate script/text name",
        messageFormat: "Duplicate {0} name \"{1}\"",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor UndeclaredConsumerCall = new(
        id: "SCPT005",
        title: "Undeclared consumer call",
        messageFormat: "Consumer call \"{0}\" is not declared in any interface",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor DuplicateInterfaceSignature = new(
        id: "SCPT006",
        title: "Duplicate interface signature",
        messageFormat: "Duplicate interface signature \"{0}\"",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor ArgumentTypeMismatch = new(
        id: "SCPT007",
        title: "Argument type mismatch",
        messageFormat: "Argument {0} of \"{1}\": cannot convert from {2} to {3}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor ArgumentCountMismatch = new(
        id: "SCPT008",
        title: "Argument count mismatch",
        messageFormat: "\"{0}\" expects {1} argument(s) but got {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
