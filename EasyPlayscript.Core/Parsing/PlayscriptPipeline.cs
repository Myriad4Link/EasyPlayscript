using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Antlr4.Runtime.Tree;

namespace EasyPlayscript.Parsing;

public static class PlayscriptPipeline
{
    public static List<ValidationDiagnostic> ProcessFile(
        StructureParseResult structureResult,
        PlayscriptCompilationData data,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        foreach (var (identifier, name, rawContent, line, col) in structureResult.Results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (rawContent == null) continue;

            var trimmedContent = rawContent.Trim('\r', '\n');
            if (string.IsNullOrEmpty(trimmedContent)) continue;

            var (parser, contentErrors) = identifier == BlockType.Script
                ? PlayscriptContentHelper.ParseScript(trimmedContent)
                : PlayscriptContentHelper.ParseText(trimmedContent);

            IParseTree tree = identifier == BlockType.Script
                ? parser.scriptContent()
                : parser.textContent();

            diagnostics.AddRange(contentErrors.Select(error =>
                new ValidationDiagnostic(error.IsLexer ? "SCPT002" : "SCPT003", error.Msg, filePath, error.Line,
                    error.Col)));

            if (contentErrors.Count > 0) continue;
            if (tree == null) continue;

            var builder = new PlayscriptCodeBuilder(cancellationToken);
            builder.Build(identifier, tree);

            diagnostics.AddRange(builder.Errors.Select(error =>
                new ValidationDiagnostic(error.IsLexer ? "SCPT002" : "SCPT003", error.Msg, filePath, error.Line,
                    error.Col)));

            if (builder.Errors.Count > 0) continue;

            if (identifier == BlockType.Script)
            {
                if (data.Scripts.ContainsKey(name))
                {
                    var loc = data.ScriptLocations[name];
                    diagnostics.Add(new ValidationDiagnostic("SCPT004",
                        $"Duplicate script name \"{name}\"",
                        loc.filePath, loc.line, loc.col, "script", name));
                }
                else
                {
                    data.ScriptLocations[name] = (filePath, line, col);
                    data.Scripts[name] = builder.ContentResult;
                }
            }
            else
            {
                if (data.Texts.ContainsKey(name))
                {
                    var loc = data.TextLocations[name];
                    diagnostics.Add(new ValidationDiagnostic("SCPT004",
                        $"Duplicate text name \"{name}\"",
                        loc.filePath, loc.line, loc.col, "text", name));
                }
                else
                {
                    data.TextLocations[name] = (filePath, line, col);
                    data.Texts[name] = builder.TextResult;
                }
            }
        }

        foreach (var iface in structureResult.Interfaces)
            iface.FilePath = filePath;
        data.Interfaces.AddRange(structureResult.Interfaces);

        return diagnostics;
    }

    public static List<ValidationDiagnostic> Validate(PlayscriptCompilationData data)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        diagnostics.AddRange(InterfaceValidator.ValidateUndeclaredCalls(data));
        diagnostics.AddRange(InterfaceValidator.ValidateDuplicateSignatures(data));
        diagnostics.AddRange(InterfaceValidator.ValidateArgumentTypes(data));
        return diagnostics;
    }

    public static List<ValidationDiagnostic> ValidateImplementations(PlayscriptCompilationData data)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        diagnostics.AddRange(ImplementationValidator.ValidateMissingImplementations(data));
        diagnostics.AddRange(ImplementationValidator.ValidateDuplicateImplementations(data));
        diagnostics.AddRange(ImplementationValidator.ValidateUnusedImplementations(data));
        return diagnostics;
    }
}