using System.Collections.Generic;
using System.Linq;

namespace EasyPlayscript.Parsing;

public static class InterfaceValidator
{
    public static IEnumerable<ConsumerCallItem> GetConsumerCalls(ScriptBlock block)
    {
        foreach (var item in from page in block.Pages
                 from paragraph in page.Paragraphs
                 from line in paragraph.Lines
                 from item in line.Items
                 select item)
            if (item is ConsumerCallItem call)
                yield return call;
    }

    public static IEnumerable<ConsumerCallItem> GetConsumerCalls(TextBlock block)
    {
        foreach (var item in from line in block.Lines
                 from item in line.Items
                 select item)
            if (item is ConsumerCallItem call)
                yield return call;
    }

    public static InterfaceType? GetArgumentType(ArgumentValue arg) =>
        arg switch
        {
            StringArgument => InterfaceType.String,
            IntArgument => InterfaceType.Int,
            DoubleArgument => InterfaceType.Decimal,
            BoolArgument => InterfaceType.Bool,
            _ => null,
        };

    public static bool IsAssignableTo(InterfaceType actual, InterfaceType expected) =>
        (actual, expected) switch
        {
            var (a, e) when a == e => true,
            (InterfaceType.Int, InterfaceType.Decimal) => true,
            _ => false,
        };

    public static string MakeSignatureKey(InterfaceDeclaration decl)
    {
        var paramTypes = string.Join(",", decl.Parameters.Select(p => 
            p.Type.ToString().ToLowerInvariant()));
        return $"{decl.Name}({paramTypes}):{decl.ReturnType.ToString().ToLowerInvariant()}";
    }

    public static List<ValidationDiagnostic> ValidateUndeclaredCalls(PlayscriptCompilationData data)
    {
        var declaredNames = new HashSet<string>(data.Interfaces.Select(i => i.Name));

        return GetAllCalls(data)
            .Where(x => !declaredNames.Contains(x.call.Identifier))
            .Select(x => new ValidationDiagnostic("SCPT005",
                $"Consumer call \"{x.call.Identifier}\" is not declared in any interface",
                x.filePath, x.call.Line, x.call.Col, x.call.Identifier))
            .ToList();
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
                var sig =
                    $"{decl.Name}({string.Join(", ", decl.Parameters.Select(p =>
                        p.Type.ToString().ToLowerInvariant()))}):{decl.ReturnType.ToString().ToLowerInvariant()}";
                errors.Add(new ValidationDiagnostic("SCPT006",
                    $"Duplicate interface signature \"{sig}\"",
                    decl.FilePath, decl.Line, decl.Col, sig));
            }
            else
                signatureMap[key] = decl;
        }

        return errors;
    }

    public static List<ValidationDiagnostic> ValidateArgumentTypes(PlayscriptCompilationData data)
    {
        var errors = new List<ValidationDiagnostic>();

        var interfacesByName = new Dictionary<string, List<InterfaceDeclaration>>();
        foreach (var decl in data.Interfaces)
        {
            if (!interfacesByName.TryGetValue(decl.Name, out var list))
            {
                list = [];
                interfacesByName[decl.Name] = list;
            }

            list.Add(decl);
        }

        foreach (var (call, filePath) in GetAllCalls(data))
            ValidateConsumerCall(call, interfacesByName, filePath, errors);

        return errors;
    }

    private static IEnumerable<(ConsumerCallItem call, string filePath)> GetAllCalls(PlayscriptCompilationData data)
    {
        foreach (var kvp in data.Scripts)
        {
            var loc = data.ScriptLocations[kvp.Key];
            foreach (var call in GetConsumerCalls(kvp.Value))
                yield return (call, loc.filePath);
        }

        foreach (var kvp in data.Texts)
        {
            var loc = data.TextLocations[kvp.Key];
            foreach (var call in GetConsumerCalls(kvp.Value))
                yield return (call, loc.filePath);
        }
    }

    private static void ValidateConsumerCall(
        ConsumerCallItem call,
        Dictionary<string, List<InterfaceDeclaration>> interfacesByName,
        string filePath,
        List<ValidationDiagnostic> errors)
    {
        if (!interfacesByName.TryGetValue(call.Identifier, out var overloads))
            return;

        var argCount = call.Arguments.Count;
        var candidates = overloads.Where(o =>
            o.Parameters.Count == argCount).ToList();

        if (candidates.Count == 0)
        {
            var expected = overloads.First().Parameters.Count;
            errors.Add(new ValidationDiagnostic("SCPT008",
                $"\"{call.Identifier}\" expects {expected} argument(s) but got {argCount}",
                filePath, call.Line, call.Col, call.Identifier, expected, argCount));
            return;
        }

        if (candidates.Any(o => TryMatchOverload(call, o)))
            return;

        var mismatchIndex = FindFirstArgMismatch(call, candidates[0]);
        if (mismatchIndex < 0) return;
        var actualType = GetArgumentType(call.Arguments[mismatchIndex]);
        var expectedType = candidates[0].Parameters[mismatchIndex].Type;
        errors.Add(new ValidationDiagnostic("SCPT007",
            $"Argument {mismatchIndex + 1} of \"{call.Identifier}\": cannot convert from {actualType?.ToString().ToLowerInvariant() ?? "unknown"} to {expectedType.ToString().ToLowerInvariant()}",
            filePath, call.Line, call.Col,
            mismatchIndex + 1, call.Identifier,
            actualType?.ToString().ToLowerInvariant() ?? "unknown",
            expectedType.ToString().ToLowerInvariant()));
    }

    private static bool TryMatchOverload(ConsumerCallItem call, InterfaceDeclaration overload)
    {
        for (var i = 0; i < call.Arguments.Count; i++)
        {
            var actualType = GetArgumentType(call.Arguments[i]);
            var expectedType = overload.Parameters[i].Type;
            if (actualType == null || !IsAssignableTo(actualType.Value, expectedType))
                return false;
        }

        return true;
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