using System.Collections.Generic;
using System.Linq;
using EasyPlayscript.DataModel;

namespace EasyPlayscript.Parsing;

public static class InterfaceValidator
{
    public static IEnumerable<ConsumerCallItem> GetConsumerCalls(ScriptBlock block)
    {
        foreach (var item in from page in block.Pages
                 from paragraph in page.Paragraphs
                 from line in paragraph.Lines
                 from segment in line.Segments
                 from item in segment.Items
                 select item)
            if (item is ConsumerCallItem call)
                yield return call;
    }

    public static IEnumerable<ConsumerCallItem> GetConsumerCalls(TextBlock block)
    {
        foreach (var item in from line in block.Lines
                 from segment in line.Segments
                 from item in segment.Items
                 select item)
            if (item is ConsumerCallItem call)
                yield return call;
    }

    public static InterfaceType? GetArgumentType(ArgumentValue arg)
    {
        return arg switch
        {
            StringArgument => InterfaceType.String,
            IntArgument => InterfaceType.Int,
            DoubleArgument => InterfaceType.Decimal,
            BoolArgument => InterfaceType.Bool,
            _ => null
        };
    }

    public static bool IsAssignableTo(InterfaceType actual, InterfaceType expected)
    {
        return (actual, expected) switch
        {
            var (a, e) when a == e => true,
            (InterfaceType.Int, InterfaceType.Decimal) => true,
            _ => false
        };
    }

    public static string MakeSignatureKey(InterfaceDeclaration decl)
    {
        var qualifiedName = PlayscriptCompilationData.GetQualifiedName(decl.Namespace, decl.Name);
        var paramTypes = string.Join(",", decl.Parameters.Select(p =>
            p.Type.ToString().ToLowerInvariant()));
        return $"{qualifiedName}({paramTypes}):{decl.ReturnType.ToString().ToLowerInvariant()}";
    }

    public static List<ValidationDiagnostic> ValidateUndeclaredCalls(PlayscriptCompilationData data)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        var declaredQualifiedNames = new HashSet<string>(
            data.Interfaces.Select(i =>
                PlayscriptCompilationData.GetQualifiedName(i.Namespace, i.Name)));

        foreach (var (call, filePath) in GetAllCalls(data))
        {
            var identifier = call.Identifier;
            if (identifier.Contains("."))
            {
                if (!declaredQualifiedNames.Contains(identifier))
                    diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.UndeclaredConsumerCall,
                        DiagnosticCodes.UndeclaredConsumerCallFormat,
                        filePath, call.Line, call.Col, identifier));
                continue;
            }

            var sameNsQualified = PlayscriptCompilationData.GetQualifiedName(call.Namespace, identifier);
            if (declaredQualifiedNames.Contains(sameNsQualified))
                continue;

            var globalMatches = declaredQualifiedNames
                .Where(n => PlayscriptCompilationData.ParseQualifiedName(n).name == identifier)
                .ToArray();

            if (globalMatches.Length == 0)
            {
                diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.UndeclaredConsumerCall,
                    DiagnosticCodes.UndeclaredConsumerCallFormat,
                    filePath, call.Line, call.Col, identifier));
            }
            else if (globalMatches.Length > 1)
            {
                diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.AmbiguousConsumerCall,
                    DiagnosticCodes.AmbiguousConsumerCallFormat,
                    filePath, call.Line, call.Col, identifier,
                    string.Join(", ", globalMatches)));
            }
            else if (globalMatches.Length == 1)
            {
                continue;
            }
        }

        return diagnostics;
    }

    public static List<ValidationDiagnostic> ValidateDuplicateSignatures(PlayscriptCompilationData data)
    {
        var errors = new List<ValidationDiagnostic>();
        var signatureMap = new Dictionary<string, InterfaceDeclaration>();

        foreach (var decl in data.Interfaces)
        {
            var key = MakeSignatureKey(decl);
            if (signatureMap.ContainsKey(key))
            {
                var qualifiedName = PlayscriptCompilationData.GetQualifiedName(decl.Namespace, decl.Name);
                var sig =
                    $"{qualifiedName}({string.Join(", ", decl.Parameters.Select(p =>
                        p.Type.ToString().ToLowerInvariant()))}):{decl.ReturnType.ToString().ToLowerInvariant()}";
                errors.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateInterfaceSignature,
                    DiagnosticCodes.DuplicateInterfaceSignatureFormat,
                    decl.FilePath, decl.Line, decl.Col, sig));
            }
            else
            {
                signatureMap[key] = decl;
            }
        }

        return errors;
    }

    public static List<ValidationDiagnostic> ValidateArgumentTypes(PlayscriptCompilationData data)
    {
        var errors = new List<ValidationDiagnostic>();

        var interfacesByQualifiedName = new Dictionary<string, List<InterfaceDeclaration>>();
        foreach (var decl in data.Interfaces)
        {
            var qualifiedName = PlayscriptCompilationData.GetQualifiedName(decl.Namespace, decl.Name);
            if (!interfacesByQualifiedName.TryGetValue(qualifiedName, out var list))
            {
                list = [];
                interfacesByQualifiedName[qualifiedName] = list;
            }

            list.Add(decl);
        }

        foreach (var (call, filePath) in GetAllCalls(data))
            ValidateConsumerCall(call, interfacesByQualifiedName, filePath, errors);

        return errors;
    }

    private static IEnumerable<(ConsumerCallItem call, string filePath)> GetAllCalls(
        PlayscriptCompilationData data)
    {
        foreach (var kvp in data.Scripts)
        {
            var qualifiedName = kvp.Key;
            var (ns, _) = PlayscriptCompilationData.ParseQualifiedName(qualifiedName);
            var variants = kvp.Value;

            if (variants.Unversioned != null)
            {
                if (data.ScriptLocations.TryGetValue((qualifiedName, ""), out var loc))
                    foreach (var call in GetConsumerCalls(variants.Unversioned))
                    {
                        call.Namespace = ns;
                        yield return (call, loc.filePath);
                    }
            }

            foreach (var vk in variants.Numbered)
            {
                var variation = vk.Key;
                if (data.ScriptLocations.TryGetValue((qualifiedName, variation), out var loc))
                    foreach (var call in GetConsumerCalls(vk.Value))
                    {
                        call.Namespace = ns;
                        yield return (call, loc.filePath);
                    }
            }
        }

        foreach (var kvp in data.Texts)
        {
            var qualifiedName = kvp.Key;
            var (ns, _) = PlayscriptCompilationData.ParseQualifiedName(qualifiedName);
            var variants = kvp.Value;

            if (variants.Unversioned != null)
            {
                if (data.TextLocations.TryGetValue((qualifiedName, ""), out var loc))
                    foreach (var call in GetConsumerCalls(variants.Unversioned))
                    {
                        call.Namespace = ns;
                        yield return (call, loc.filePath);
                    }
            }

            foreach (var vk in variants.Numbered)
            {
                var variation = vk.Key;
                if (data.TextLocations.TryGetValue((qualifiedName, variation), out var loc))
                    foreach (var call in GetConsumerCalls(vk.Value))
                    {
                        call.Namespace = ns;
                        yield return (call, loc.filePath);
                    }
            }
        }
    }

    private static string FormatCandidates(List<InterfaceDeclaration> candidates)
    {
        var signatures = candidates.Select(c =>
        {
            var qualifiedName = PlayscriptCompilationData.GetQualifiedName(c.Namespace, c.Name);
            return $"{qualifiedName}({string.Join(", ", c.Parameters.Select(p => p.Type.ToString().ToLowerInvariant()))}):{c.ReturnType.ToString().ToLowerInvariant()}";
        });
        return "\n  Candidates:\n    " + string.Join("\n    ", signatures);
    }

    private static void ValidateConsumerCall(
        ConsumerCallItem call,
        Dictionary<string, List<InterfaceDeclaration>> interfacesByQualifiedName,
        string filePath,
        List<ValidationDiagnostic> errors)
    {
        var resolvedName = ResolveInterfaceName(call.Identifier, call.Namespace, interfacesByQualifiedName);
        if (resolvedName == null)
            return;

        if (!interfacesByQualifiedName.TryGetValue(resolvedName, out var overloads))
            return;

        var argCount = call.Arguments.Count;
        var candidates = overloads.Where(o =>
            o.Parameters.Count == argCount).ToList();

        if (candidates.Count == 0)
        {
            var candidateSuffix = overloads.Count > 1 ? FormatCandidates(overloads) : "";
            errors.Add(new ValidationDiagnostic(DiagnosticCodes.ArgumentCountMismatch,
                DiagnosticCodes.ArgumentCountMismatchFormat,
                filePath, call.Line, call.Col, call.Identifier, argCount, candidateSuffix));
            return;
        }

        if (candidates.Any(o => TryMatchOverload(call, o)))
            return;

        var mismatchIndex = FindFirstArgMismatch(call, candidates[0]);
        if (mismatchIndex < 0) return;
        var actualType = GetArgumentType(call.Arguments[mismatchIndex]);
        var expectedType = candidates[0].Parameters[mismatchIndex].Type;
        var candidateSuffix2 = candidates.Count > 1 ? FormatCandidates(candidates) : "";
        errors.Add(new ValidationDiagnostic(DiagnosticCodes.ArgumentTypeMismatch,
            DiagnosticCodes.ArgumentTypeMismatchFormat,
            filePath, call.Line, call.Col,
            mismatchIndex + 1, call.Identifier,
            actualType?.ToString().ToLowerInvariant() ?? "unknown",
            expectedType.ToString().ToLowerInvariant(),
            candidateSuffix2));
    }

    private static string? ResolveInterfaceName(
        string callIdentifier,
        string? callNamespace,
        Dictionary<string, List<InterfaceDeclaration>> interfacesByQualifiedName)
    {
        if (callIdentifier.Contains("."))
        {
            if (interfacesByQualifiedName.ContainsKey(callIdentifier))
                return callIdentifier;
            return null;
        }

        var sameNsQualified = PlayscriptCompilationData.GetQualifiedName(callNamespace, callIdentifier);
        if (interfacesByQualifiedName.ContainsKey(sameNsQualified))
            return sameNsQualified;

        var globalMatches = interfacesByQualifiedName.Keys
            .Where(k => PlayscriptCompilationData.ParseQualifiedName(k).name == callIdentifier)
            .ToArray();

        if (globalMatches.Length == 1)
            return globalMatches[0];

        return null;
    }

    private static bool TryMatchOverload(ConsumerCallItem call, InterfaceDeclaration overload)
    {
        return FindFirstArgMismatch(call, overload) < 0;
    }

    private static int FindFirstArgMismatch(ConsumerCallItem call, InterfaceDeclaration overload)
    {
        for (var i = 0; i < call.Arguments.Count; i++)
        {
            var actualType = GetArgumentType(call.Arguments[i]);
            var expectedType = overload.Parameters[i].Type;
            if (actualType == null || !IsAssignableTo(actualType.Value, expectedType))
                return i;
        }

        return -1;
    }
}
