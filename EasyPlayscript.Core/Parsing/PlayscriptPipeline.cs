using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Antlr4.Runtime.Tree;
using EasyPlayscript.DataModel;

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

        foreach (var result in structureResult.Results)
        {
            var identifier = result.Identifier;
            var name = result.Name;
            var variation = result.Variation;
            var isDefault = result.IsDefault;
            var rawContent = result.RawContent;
            var line = result.Line;
            var col = result.Col;

            cancellationToken.ThrowIfCancellationRequested();
            if (rawContent == null) continue;

            var trimmedContent = rawContent.Trim('\r', '\n');
            if (string.IsNullOrEmpty(trimmedContent)) continue;

            var blockDiagnostics = ProcessBlock(identifier, trimmedContent, filePath, cancellationToken,
                out var builder, out var contentFailed);

            diagnostics.AddRange(blockDiagnostics);

            if (contentFailed || builder is null) continue;

            RegisterBlock(data, identifier, name, variation, isDefault, builder, filePath, line, col, diagnostics);
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
        string? variation,
        bool isDefault,
        PlayscriptCodeBuilder builder,
        string filePath,
        int line,
        int col,
        List<ValidationDiagnostic> diagnostics)
    {
        if (identifier == BlockType.Script)
        {
            if (!data.Scripts.TryGetValue(name, out var variants))
            {
                variants = new ScriptVariants();
                data.Scripts[name] = variants;
            }

            if (variation == null)
            {
                if (variants.Unversioned != null)
                {
                    var locKey = (name, "");
                    var loc = data.ScriptLocations[locKey];
                    diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateScriptName,
                        DiagnosticCodes.DuplicateScriptNameFormat,
                        loc.filePath, loc.line, loc.col, "script", name));
                    return;
                }

                data.ScriptLocations[(name, "")] = (filePath, line, col);
                variants.Unversioned = builder.ContentResult;
            }
            else
            {
                if (isDefault)
                {
                    if (variants.Unversioned != null)
                    {
                        var defKey = (name, "");
                        var loc = data.ScriptLocations[defKey];
                        diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateScriptName,
                            DiagnosticCodes.DuplicateScriptNameFormat,
                            loc.filePath, loc.line, loc.col, "script", $"{name} (default variation {variation})"));
                        return;
                    }

                    variants.Unversioned = builder.ContentResult;
                    data.ScriptLocations[(name, "")] = (filePath, line, col);
                }

                var locKey = (name, variation);
                if (variants.Numbered.ContainsKey(variation))
                {
                    var loc = data.ScriptLocations[locKey];
                    diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateScriptName,
                        DiagnosticCodes.DuplicateScriptNameFormat,
                        loc.filePath, loc.line, loc.col, "script", $"{name} variation {variation}"));
                    return;
                }

                data.ScriptLocations[locKey] = (filePath, line, col);
                variants.Numbered[variation] = builder.ContentResult;
            }
        }
        else
        {
            if (!data.Texts.TryGetValue(name, out var variants))
            {
                variants = new TextVariants();
                data.Texts[name] = variants;
            }

            if (variation == null)
            {
                if (variants.Unversioned != null)
                {
                    var locKey = (name, "");
                    var loc = data.TextLocations[locKey];
                    diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateScriptName,
                        DiagnosticCodes.DuplicateScriptNameFormat,
                        loc.filePath, loc.line, loc.col, "text", name));
                    return;
                }

                data.TextLocations[(name, "")] = (filePath, line, col);
                variants.Unversioned = builder.TextResult;
            }
            else
            {
                if (isDefault)
                {
                    if (variants.Unversioned != null)
                    {
                        var defKey = (name, "");
                        var loc = data.TextLocations[defKey];
                        diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateScriptName,
                            DiagnosticCodes.DuplicateScriptNameFormat,
                            loc.filePath, loc.line, loc.col, "text", $"{name} (default variation {variation})"));
                        return;
                    }

                    variants.Unversioned = builder.TextResult;
                    data.TextLocations[(name, "")] = (filePath, line, col);
                }

                var locKey = (name, variation);
                if (variants.Numbered.ContainsKey(variation))
                {
                    var loc = data.TextLocations[locKey];
                    diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateScriptName,
                        DiagnosticCodes.DuplicateScriptNameFormat,
                        loc.filePath, loc.line, loc.col, "text", $"{name} variation {variation}"));
                    return;
                }

                data.TextLocations[locKey] = (filePath, line, col);
                variants.Numbered[variation] = builder.TextResult;
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
