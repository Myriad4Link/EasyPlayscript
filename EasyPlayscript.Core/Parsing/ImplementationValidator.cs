using System.Collections.Generic;
using System.Linq;

namespace EasyPlayscript.Parsing;

public static class ImplementationValidator
{
    public static List<ValidationDiagnostic> ValidateMissingImplementations(PlayscriptCompilationData data)
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
            if (!implLookup.TryGetValue(key, out var impl))
            {
                errors.Add(new ValidationDiagnostic(DiagnosticCodes.MissingImplementation,
                    DiagnosticCodes.MissingImplementationFormat,
                    iface.FilePath, iface.Line, iface.Col, iface.Name));
            }
            else if (iface.IsAsync && !impl.IsAsync)
            {
                errors.Add(new ValidationDiagnostic(DiagnosticCodes.AsyncSyncMismatch,
                    DiagnosticCodes.AsyncSyncMismatchFormat,
                    iface.FilePath, iface.Line, iface.Col, iface.Name));
            }
            else if (!iface.IsAsync && impl.IsAsync)
            {
                errors.Add(new ValidationDiagnostic(DiagnosticCodes.SyncAsyncMismatch,
                    DiagnosticCodes.SyncAsyncMismatchFormat,
                    iface.FilePath, iface.Line, iface.Col, iface.Name));
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

                errors.AddRange(group.Skip(1).Select(impl =>
                    new ValidationDiagnostic(DiagnosticCodes.DuplicateImplementation,
                        DiagnosticCodes.DuplicateImplementationFormat, impl.FilePath, impl.Line, 0, name, paramCount,
                        impl.ClassName)));
            }
        }

        return errors;
    }

    public static List<ValidationDiagnostic> ValidateUnusedImplementations(PlayscriptCompilationData data)
    {
        var usedNames = new HashSet<string>();
        foreach (var variants in data.Scripts.Select(kvp => kvp.Value))
        {
            if (variants.Unversioned != null)
                foreach (var call in InterfaceValidator.GetConsumerCalls(variants.Unversioned))
                    usedNames.Add(call.Identifier);
            foreach (var call in
                     variants.Numbered.Values.SelectMany(InterfaceValidator.GetConsumerCalls))
                usedNames.Add(call.Identifier);
        }

        foreach (var variants in data.Texts.Select(kvp => kvp.Value))
        {
            if (variants.Unversioned != null)
                foreach (var call in InterfaceValidator.GetConsumerCalls(variants.Unversioned))
                    usedNames.Add(call.Identifier);
            foreach (var call in
                     variants.Numbered.Values.SelectMany(InterfaceValidator.GetConsumerCalls))
                usedNames.Add(call.Identifier);
        }

        return (from impl in data.Implementations
            where !usedNames.Contains(impl.EffectiveName)
            select new ValidationDiagnostic(DiagnosticCodes.UnusedImplementation,
                DiagnosticCodes.UnusedImplementationFormat, impl.FilePath, impl.Line, 0, impl.ClassName,
                impl.MethodName)).ToList();
    }
}