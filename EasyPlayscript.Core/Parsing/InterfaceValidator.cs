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
        foreach (var item in block.Items)
            if (item is ConsumerCallItem call)
                yield return call;
    }

    public static InterfaceType? GetArgumentType(ArgumentValue arg)
    {
        if (arg is StringArgument) return InterfaceType.String;
        if (arg is IntArgument) return InterfaceType.Int;
        if (arg is DoubleArgument) return InterfaceType.Decimal;
        if (arg is BoolArgument) return InterfaceType.Bool;
        return null;
    }

    public static bool IsAssignableTo(InterfaceType actual, InterfaceType expected)
    {
        if (actual == expected) return true;
        if (actual == InterfaceType.Int && expected == InterfaceType.Decimal) return true;
        return false;
    }

    public static string MakeSignatureKey(InterfaceDeclaration decl)
    {
        var paramTypes = string.Join(",", decl.Parameters.Select(p => p.Type.ToString().ToLowerInvariant()));
        return $"{decl.Name}({paramTypes}):{decl.ReturnType.ToString().ToLowerInvariant()}";
    }

    public static List<ValidationDiagnostic> ValidateUndeclaredCalls(
        IList<InterfaceDeclaration> interfaces,
        IDictionary<string, ScriptBlock> scripts,
        IDictionary<string, (string filePath, int line, int col)> scriptLocations,
        IDictionary<string, TextBlock> texts,
        IDictionary<string, (string filePath, int line, int col)> textLocations)
    {
        var errors = new List<ValidationDiagnostic>();
        var declaredNames = new HashSet<string>(interfaces.Select(i => i.Name));

        foreach (var kvp in scripts)
        {
            var loc = scriptLocations[kvp.Key];
            foreach (var call in GetConsumerCalls(kvp.Value))
            {
                if (!declaredNames.Contains(call.Identifier))
                    errors.Add(new ValidationDiagnostic("SCPT005",
                        $"Consumer call \"{call.Identifier}\" is not declared in any interface",
                        loc.filePath, call.Line, call.Col, call.Identifier));
            }
        }

        foreach (var kvp in texts)
        {
            var loc = textLocations[kvp.Key];
            foreach (var call in GetConsumerCalls(kvp.Value))
            {
                if (!declaredNames.Contains(call.Identifier))
                    errors.Add(new ValidationDiagnostic("SCPT005",
                        $"Consumer call \"{call.Identifier}\" is not declared in any interface",
                        loc.filePath, call.Line, call.Col, call.Identifier));
            }
        }

        return errors;
    }

    public static List<ValidationDiagnostic> ValidateDuplicateSignatures(
        IList<InterfaceDeclaration> interfaces)
    {
        var errors = new List<ValidationDiagnostic>();
        var signatureMap = new Dictionary<string, InterfaceDeclaration>();

        foreach (var decl in interfaces)
        {
            var key = MakeSignatureKey(decl);
            if (signatureMap.ContainsKey(key))
            {
                var sig =
                    $"{decl.Name}({string.Join(", ", decl.Parameters.Select(p => p.Type.ToString().ToLowerInvariant()))}):{decl.ReturnType.ToString().ToLowerInvariant()}";
                errors.Add(new ValidationDiagnostic("SCPT006",
                    $"Duplicate interface signature \"{sig}\"",
                    decl.FilePath, decl.Line, decl.Col, sig));
            }
            else
                signatureMap[key] = decl;
        }

        return errors;
    }

    public static List<ValidationDiagnostic> ValidateArgumentTypes(
        IList<InterfaceDeclaration> interfaces,
        IDictionary<string, ScriptBlock> scripts,
        IDictionary<string, (string filePath, int line, int col)> scriptLocations,
        IDictionary<string, TextBlock> texts,
        IDictionary<string, (string filePath, int line, int col)> textLocations)
    {
        var errors = new List<ValidationDiagnostic>();

        var interfacesByName = new Dictionary<string, List<InterfaceDeclaration>>();
        foreach (var decl in interfaces)
        {
            if (!interfacesByName.TryGetValue(decl.Name, out var list))
            {
                list = new List<InterfaceDeclaration>();
                interfacesByName[decl.Name] = list;
            }

            list.Add(decl);
        }

        foreach (var kvp in scripts)
        {
            var loc = scriptLocations[kvp.Key];
            foreach (var call in GetConsumerCalls(kvp.Value))
                ValidateConsumerCall(call, interfacesByName, loc.filePath, errors);
        }

        foreach (var kvp in texts)
        {
            var loc = textLocations[kvp.Key];
            foreach (var call in GetConsumerCalls(kvp.Value))
                ValidateConsumerCall(call, interfacesByName, loc.filePath, errors);
        }

        return errors;
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
        var matchingCount = overloads.Where(o => o.Parameters.Count == argCount).ToList();

        if (matchingCount.Count == 0)
        {
            var expected = overloads.First().Parameters.Count;
            errors.Add(new ValidationDiagnostic("SCPT008",
                $"\"{call.Identifier}\" expects {expected} argument(s) but got {argCount}",
                filePath, call.Line, call.Col, call.Identifier, expected, argCount));
            return;
        }

        if (matchingCount.Any(o => TryMatchOverload(call, o)))
            return;

        var mismatchIndex = FindFirstArgMismatch(call, matchingCount[0]);
        if (mismatchIndex >= 0)
        {
            var actualType = GetArgumentType(call.Arguments[mismatchIndex]);
            var expectedType = matchingCount[0].Parameters[mismatchIndex].Type;
            errors.Add(new ValidationDiagnostic("SCPT007",
                $"Argument {mismatchIndex + 1} of \"{call.Identifier}\": cannot convert from {actualType?.ToString().ToLowerInvariant() ?? "unknown"} to {expectedType.ToString().ToLowerInvariant()}",
                filePath, call.Line, call.Col,
                mismatchIndex + 1, call.Identifier,
                actualType?.ToString().ToLowerInvariant() ?? "unknown",
                expectedType.ToString().ToLowerInvariant()));
        }
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