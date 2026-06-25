using System;
using System.Collections.Generic;
using EasyPlayscript.Parsing;
using Microsoft.CodeAnalysis;

namespace EasyPlayscript.Generator;

/// <summary>
///     Diagnostic descriptors for playscript parsing.
/// </summary>
internal static class PlayscriptDiagnostics
{
    private const string Category = "Playscript";

    internal static readonly DiagnosticDescriptor UnexpectedToken = new(
        DiagnosticCodes.UnexpectedToken,
        "Unexpected token",
        DiagnosticCodes.UnexpectedTokenFormat,
        Category,
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor MismatchedInput = new(
        DiagnosticCodes.MismatchedInput,
        "Mismatched input",
        DiagnosticCodes.MismatchedInputFormat,
        Category,
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor DuplicateScriptName = new(
        DiagnosticCodes.DuplicateScriptName,
        "Duplicate script/text name",
        DiagnosticCodes.DuplicateScriptNameFormat,
        Category,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UndeclaredConsumerCall = new(
        DiagnosticCodes.UndeclaredConsumerCall,
        "Undeclared consumer call",
        DiagnosticCodes.UndeclaredConsumerCallFormat,
        Category,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateInterfaceSignature = new(
        DiagnosticCodes.DuplicateInterfaceSignature,
        "Duplicate interface signature",
        DiagnosticCodes.DuplicateInterfaceSignatureFormat,
        Category,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor ArgumentTypeMismatch = new(
        DiagnosticCodes.ArgumentTypeMismatch,
        "Argument type mismatch",
        DiagnosticCodes.ArgumentTypeMismatchFormat,
        Category,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor ArgumentCountMismatch = new(
        DiagnosticCodes.ArgumentCountMismatch,
        "Argument count mismatch",
        DiagnosticCodes.ArgumentCountMismatchFormat,
        Category,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingImplementation = new(
        DiagnosticCodes.MissingImplementation,
        "Missing implementation",
        DiagnosticCodes.MissingImplementationFormat,
        Category,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateImplementation = new(
        DiagnosticCodes.DuplicateImplementation,
        "Duplicate implementation",
        DiagnosticCodes.DuplicateImplementationFormat,
        Category,
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnusedImplementation = new(
        DiagnosticCodes.UnusedImplementation,
        "Unused implementation",
        DiagnosticCodes.UnusedImplementationFormat,
        Category,
        DiagnosticSeverity.Warning,
        true);

    private static readonly Dictionary<string, DiagnosticDescriptor> ByCodeMap = new()
    {
        [DiagnosticCodes.UnexpectedToken] = UnexpectedToken,
        [DiagnosticCodes.MismatchedInput] = MismatchedInput,
        [DiagnosticCodes.DuplicateScriptName] = DuplicateScriptName,
        [DiagnosticCodes.UndeclaredConsumerCall] = UndeclaredConsumerCall,
        [DiagnosticCodes.DuplicateInterfaceSignature] = DuplicateInterfaceSignature,
        [DiagnosticCodes.ArgumentTypeMismatch] = ArgumentTypeMismatch,
        [DiagnosticCodes.ArgumentCountMismatch] = ArgumentCountMismatch,
        [DiagnosticCodes.MissingImplementation] = MissingImplementation,
        [DiagnosticCodes.DuplicateImplementation] = DuplicateImplementation,
        [DiagnosticCodes.UnusedImplementation] = UnusedImplementation
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