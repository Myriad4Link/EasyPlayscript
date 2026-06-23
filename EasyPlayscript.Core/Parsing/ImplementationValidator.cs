using System.Collections.Generic;
using System.Linq;

namespace EasyPlayscript.Parsing;

public static class ImplementationValidator
{
    public static List<ValidationDiagnostic> ValidateMissingImplementations(PlayscriptCompilationData data)
    {
        var errors = new List<ValidationDiagnostic>();

        var implLookup = new HashSet<string>(
            data.Implementations.Select(i => $"{i.EffectiveName}:{i.ParameterTypeNames.Count}"));

        foreach (var iface in data.Interfaces)
        {
            var key = $"{iface.Name}:{iface.Parameters.Count}";
            if (!implLookup.Contains(key))
            {
                errors.Add(new ValidationDiagnostic("SCPT009",
                    $"Interface \"{iface.Name}\" has no [Implementation] method",
                    iface.FilePath, iface.Line, iface.Col, iface.Name, ""));
            }
        }

        return errors;
    }

    public static List<ValidationDiagnostic> ValidateDuplicateImplementations(PlayscriptCompilationData data)
    {
        var errors = new List<ValidationDiagnostic>();

        var groups = data.Implementations
            .GroupBy(i => $"{i.EffectiveName}:{i.ParameterTypeNames.Count}")
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            var classNames = group.Select(i => i.ClassName).Distinct().ToList();
            if (classNames.Count > 1)
            {
                var first = group.First();
                var name = first.EffectiveName;
                var paramCount = first.ParameterTypeNames.Count;
                var classList = string.Join(", ", classNames);

                foreach (var impl in group.Skip(1))
                {
                    errors.Add(new ValidationDiagnostic("SCPT010",
                        $"Duplicate [Implementation] for \"{name}\" with {paramCount} parameter(s) in {impl.ClassName}",
                        impl.FilePath, impl.Line, 0, name, paramCount, impl.ClassName));
                }
            }
        }

        return errors;
    }

    public static List<ValidationDiagnostic> ValidateImplementationArgCounts(PlayscriptCompilationData data)
    {
        var errors = new List<ValidationDiagnostic>();

        var implLookup = new Dictionary<string, ImplementationInfo>();
        foreach (var impl in data.Implementations)
        {
            var key = $"{impl.EffectiveName}:{impl.ParameterTypeNames.Count}";
            if (!implLookup.ContainsKey(key))
                implLookup[key] = impl;
        }

        foreach (var iface in data.Interfaces)
        {
            var key = $"{iface.Name}:{iface.Parameters.Count}";
            if (implLookup.TryGetValue(key, out var impl))
            {
                if (impl.ParameterTypeNames.Count != iface.Parameters.Count)
                {
                    errors.Add(new ValidationDiagnostic("SCPT012",
                        $"[Implementation] method \"{impl.ClassName}.{impl.MethodName}\" has {impl.ParameterTypeNames.Count} parameter(s) but interface \"{iface.Name}\" expects {iface.Parameters.Count}",
                        impl.FilePath, impl.Line, 0,
                        impl.ClassName, impl.MethodName, impl.ParameterTypeNames.Count,
                        iface.Name, iface.Parameters.Count));
                }
            }
        }

        return errors;
    }

    public static List<ValidationDiagnostic> ValidateUnusedImplementations(PlayscriptCompilationData data)
    {
        var warnings = new List<ValidationDiagnostic>();

        var usedNames = new HashSet<string>();
        foreach (var kvp in data.Scripts)
        {
            if (!data.ScriptLocations.ContainsKey(kvp.Key)) continue;
            foreach (var call in InterfaceValidator.GetConsumerCalls(kvp.Value))
                usedNames.Add(call.Identifier);
        }

        foreach (var kvp in data.Texts)
        {
            if (!data.TextLocations.ContainsKey(kvp.Key)) continue;
            foreach (var call in InterfaceValidator.GetConsumerCalls(kvp.Value))
                usedNames.Add(call.Identifier);
        }

        foreach (var impl in data.Implementations)
        {
            if (!usedNames.Contains(impl.EffectiveName))
            {
                warnings.Add(new ValidationDiagnostic("SCPT011",
                    $"[Implementation] method \"{impl.ClassName}.{impl.MethodName}\" is not used by any playscript",
                    impl.FilePath, impl.Line, 0, impl.ClassName, impl.MethodName));
            }
        }

        return warnings;
    }
}
