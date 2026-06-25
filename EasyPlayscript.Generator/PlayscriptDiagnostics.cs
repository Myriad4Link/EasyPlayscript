using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace EasyPlayscript.Generator;

/// <summary>
///     Diagnostic descriptors for playscript parsing.
/// </summary>
internal static class PlayscriptDiagnostics
{
    private const string Category = "Playscript";

    internal static readonly DiagnosticDescriptor UnexpectedToken = new(
        "SCPT002",
        "Unexpected token",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor MismatchedInput = new(
        "SCPT003",
        "Mismatched input",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor DuplicateScriptName = new(
        "SCPT004",
        "Duplicate script/text name",
        "Duplicate {0} name \"{1}\"",
        Category,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UndeclaredConsumerCall = new(
        "SCPT005",
        "Undeclared consumer call",
        "Consumer call \"{0}\" is not declared in any interface",
        Category,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateInterfaceSignature = new(
        "SCPT006",
        "Duplicate interface signature",
        "Duplicate interface signature \"{0}\"",
        Category,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor ArgumentTypeMismatch = new(
        "SCPT007",
        "Argument type mismatch",
        "Argument {0} of \"{1}\": cannot convert from {2} to {3}{4}",
        Category,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor ArgumentCountMismatch = new(
        "SCPT008",
        "Argument count mismatch",
        "\"{0}\" does not match any overload with {1} argument(s){2}",
        Category,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingImplementation = new(
        "SCPT009",
        "Missing implementation",
        "Interface \"{0}\" has no [Implementation] method{1}",
        Category,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateImplementation = new(
        "SCPT010",
        "Duplicate implementation",
        "Duplicate [Implementation] for \"{0}\" with {1} parameter(s) in {2}",
        Category,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnusedImplementation = new(
        "SCPT011",
        "Unused implementation",
        "[Implementation] method \"{0}.{1}\" is not used by any playscript",
        Category,
        DiagnosticSeverity.Warning,
        true);

    private static readonly Dictionary<string, DiagnosticDescriptor> ByCodeMap = new()
    {
        ["SCPT002"] = UnexpectedToken,
        ["SCPT003"] = MismatchedInput,
        ["SCPT004"] = DuplicateScriptName,
        ["SCPT005"] = UndeclaredConsumerCall,
        ["SCPT006"] = DuplicateInterfaceSignature,
        ["SCPT007"] = ArgumentTypeMismatch,
        ["SCPT008"] = ArgumentCountMismatch,
        ["SCPT009"] = MissingImplementation,
        ["SCPT010"] = DuplicateImplementation,
        ["SCPT011"] = UnusedImplementation
    };

    internal static DiagnosticDescriptor GetDescriptor(string code)
    {
        if (ByCodeMap.TryGetValue(code, out var descriptor))
            return descriptor;

        throw new InvalidOperationException(
            $"Missing DiagnosticDescriptor for code \"{code}\". " +
            $"Add it to {nameof(PlayscriptDiagnostics)}.{nameof(ByCodeMap)} to keep validation codes in sync.");
    }
}