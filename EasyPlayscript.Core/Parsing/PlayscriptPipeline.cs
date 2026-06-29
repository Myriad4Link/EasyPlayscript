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

            var blockDiagnostics = ProcessBlock(identifier, trimmedContent, filePath, cancellationToken,
                out var builder, out var contentFailed);

            diagnostics.AddRange(blockDiagnostics);

            if (contentFailed || builder is null) continue;

            RegisterBlock(data, identifier, name, builder, filePath, line, col, diagnostics);
        }

        foreach (var iface in structureResult.Interfaces)
            iface.FilePath = filePath;
        data.Interfaces.AddRange(structureResult.Interfaces);

        return diagnostics;
    }

    private static List<ValidationDiagnostic> ProcessBlock(
        BlockType identifier,
        string trimmedContent,
        string filePath,
        CancellationToken cancellationToken,
        out PlayscriptCodeBuilder? builder,
        out bool contentFailed)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        builder = null;
        contentFailed = false;

        var (parser, contentErrors) = identifier == BlockType.Script
            ? PlayscriptContentHelper.ParseScript(trimmedContent)
            : PlayscriptContentHelper.ParseText(trimmedContent);

        IParseTree tree = identifier == BlockType.Script
            ? parser.scriptContent()
            : parser.textContent();

        diagnostics.AddRange(ToDiagnostics(contentErrors, filePath));

        if (contentErrors.Count > 0) { contentFailed = true; return diagnostics; }
        if (tree == null) { contentFailed = true; return diagnostics; }

        builder = new PlayscriptCodeBuilder(cancellationToken);
        builder.Build(identifier, tree);

        diagnostics.AddRange(ToDiagnostics(builder.Errors, filePath));

        if (builder.Errors.Count > 0) { contentFailed = true; builder = null; }

        return diagnostics;
    }

    private static void RegisterBlock(
        PlayscriptCompilationData data,
        BlockType identifier,
        string name,
        PlayscriptCodeBuilder builder,
        string filePath,
        int line,
        int col,
        List<ValidationDiagnostic> diagnostics)
    {
        if (identifier == BlockType.Script)
        {
            if (data.Scripts.ContainsKey(name))
            {
                var loc = data.ScriptLocations[name];
                diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateScriptName,
                    DiagnosticCodes.DuplicateScriptNameFormat,
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
                diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateScriptName,
                    DiagnosticCodes.DuplicateScriptNameFormat,
                    loc.filePath, loc.line, loc.col, "text", name));
            }
            else
            {
                data.TextLocations[name] = (filePath, line, col);
                data.Texts[name] = builder.TextResult;
            }
        }
    }

    private static List<ValidationDiagnostic> ToDiagnostics(
        IReadOnlyList<PlayscriptError> errors, string filePath)
    {
        return errors.Select(error =>
            ValidationDiagnostic.CreateRaw(
                error.IsLexer ? DiagnosticCodes.UnexpectedToken : DiagnosticCodes.MismatchedInput, error.Msg,
                filePath, error.Line,
                error.Col)).ToList();
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