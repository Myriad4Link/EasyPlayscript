using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using EasyPlayscript.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace EasyPlayscript;

/// <summary>
/// Incremental source generator that reads all <c>.scpt</c> files, parses them via two-pass ANTLR,
/// and emits a single <c>Registry</c> class with static properties for each script/text.
/// </summary>
[Generator]
public class PlayscriptGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var scptProvider = context.AdditionalTextsProvider
            .Where(static f => f.Path.EndsWith(".scpt"))
            .Select(static (file, ct) => ParseSingleFile(file.Path, file.GetText(ct)?.ToString() ?? string.Empty, ct));

        var allFilesProvider = scptProvider.Collect();
        var combinedProvider = context.AnalyzerConfigOptionsProvider.Combine(allFilesProvider);

        context.RegisterSourceOutput(combinedProvider, static (spc, combined) =>
        {
            var (configOptions, allResults) = combined;

            var ctx = new GeneratorContext(spc);

            foreach (var diag in allResults.SelectMany(result => result.diagnostics))
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                spc.ReportDiagnostic(diag);
                if (diag.Severity == DiagnosticSeverity.Error)
                    ctx.Data.HasErrors = true;
            }

            MergeBlocks(allResults, ctx);
            ReportValidationDiagnostics(ctx);

            if (ctx.Data.HasErrors)
                return;

            spc.CancellationToken.ThrowIfCancellationRequested();

            configOptions.GlobalOptions.TryGetValue("build_property.PlayscriptOutputPath", out var outputPath);
            configOptions.GlobalOptions.TryGetValue("build_property.PlayscriptAesKey", out var aesKey);

            outputPath = string.IsNullOrEmpty(outputPath) ? "playscripts.bin" : outputPath;
            aesKey = string.IsNullOrEmpty(aesKey) ? "dev-key-change-me" : aesKey;

            var code = RegistryEmitter.Generate(ctx.Data.Scripts, ctx.Data.Texts, outputPath!, aesKey!);

            spc.CancellationToken.ThrowIfCancellationRequested();
            spc.AddSource("Registry.g.cs", SourceText.From(code, Encoding.UTF8));
        });
    }

    private sealed class GeneratorContext(SourceProductionContext spc)
    {
        public PlayscriptCompilationData Data { get; } = new();

        public void ReportDiagnostic(Diagnostic diag)
        {
            spc.ReportDiagnostic(diag);
            Data.HasErrors = true;
        }
    }

    private static (
        List<(string name, ScriptBlock block, int line, int col)> scriptBlocks,
        List<(string name, TextBlock block, int line, int col)> textBlocks,
        List<InterfaceDeclaration> interfaces,
        string filePath,
        List<Diagnostic> diagnostics)
        ParseSingleFile(string filePath, string content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var structureResults = PlayscriptStructureHelper.ParseStructureWithErrors(content);

        var diagnostics = new List<Diagnostic>();
        var scriptBlocks = new List<(string name, ScriptBlock block, int line, int col)>();
        var textBlocks = new List<(string name, TextBlock block, int line, int col)>();

        AddContentDiagnostics(diagnostics, structureResults.errors, filePath, ct);

        foreach (var (identifier, name, rawContent, line, col) in structureResults.result.Results)
        {
            ct.ThrowIfCancellationRequested();
            if (rawContent == null) continue;

            var trimmedContent = rawContent.Trim('\r', '\n');
            var (parser, contentErrors) = PlayscriptContentHelper.Parse(trimmedContent);
            var tree = parser.scriptContent();

            AddContentDiagnostics(diagnostics, contentErrors, filePath, ct);
            if (contentErrors.Count > 0) continue;

            ct.ThrowIfCancellationRequested();
            var builder = new PlayscriptCodeBuilder(ct);

            if (TryBuildContent(builder, tree, identifier == BlockType.Script,
                    diagnostics, filePath, ct, out var sb, out var tb))
            {
                if (sb != null)
                    scriptBlocks.Add((name, sb, line, col));
                else
                    textBlocks.Add((name, tb!, line, col));
            }
        }

        var interfaces = structureResults.result.Interfaces;
        foreach (var i in interfaces)
            i.FilePath = filePath;

        return (scriptBlocks, textBlocks, interfaces, filePath, diagnostics);
    }

    private static void MergeBlocks(
        ImmutableArray<(List<(string name, ScriptBlock block, int line, int col)> scriptBlocks,
            List<(string name, TextBlock block, int line, int col)> textBlocks,
            List<InterfaceDeclaration> interfaces, string filePath, List<Diagnostic> diagnostics)> allResults,
        GeneratorContext ctx)
    {
        foreach (var result in allResults)
        {
            ctx.Data.Interfaces.AddRange(result.interfaces);

            foreach (var (name, block, line, col) in result.scriptBlocks)
            {
                if (ctx.Data.Scripts.ContainsKey(name))
                {
                    var loc = ctx.Data.ScriptLocations[name];
                    ctx.ReportDiagnostic(Diagnostic.Create(PlayscriptDiagnostics.DuplicateScriptName,
                        MakeLocation(loc.filePath, loc.line, loc.col),
                        "script", name));
                }
                else
                    ctx.Data.ScriptLocations[name] = (result.filePath, line, col);

                ctx.Data.Scripts[name] = block;
            }

            foreach (var (name, block, line, col) in result.textBlocks)
            {
                if (ctx.Data.Texts.ContainsKey(name))
                {
                    var loc = ctx.Data.TextLocations[name];
                    ctx.ReportDiagnostic(Diagnostic.Create(PlayscriptDiagnostics.DuplicateScriptName,
                        MakeLocation(loc.filePath, loc.line, loc.col),
                        "text", name));
                }
                else
                    ctx.Data.TextLocations[name] = (result.filePath, line, col);

                ctx.Data.Texts[name] = block;
            }
        }
    }

    private static void ReportValidationDiagnostics(GeneratorContext ctx)
    {
        var allDiagnostics = new List<ValidationDiagnostic>();
        allDiagnostics.AddRange(InterfaceValidator.ValidateUndeclaredCalls(ctx.Data));
        allDiagnostics.AddRange(InterfaceValidator.ValidateDuplicateSignatures(ctx.Data));
        allDiagnostics.AddRange(InterfaceValidator.ValidateArgumentTypes(ctx.Data));

        foreach (var diag in allDiagnostics)
        {
            var descriptor = PlayscriptDiagnostics.GetDescriptor(diag.Code);
            ctx.ReportDiagnostic(Diagnostic.Create(descriptor,
                MakeLocation(diag.FilePath, diag.Line, diag.Col), diag.MessageArgs));
        }
    }

    private static Location MakeLocation(string filePath, int line, int col)
    {
        var linePosition = new LinePosition(line - 1, col);
        return Location.Create(filePath, default, new LinePositionSpan(linePosition, linePosition));
    }

    private static void AddContentDiagnostics(
        List<Diagnostic> diagnostics,
        List<PlayscriptError> errors,
        string filePath,
        CancellationToken ct)
    {
        foreach (var error in errors)
        {
            ct.ThrowIfCancellationRequested();
            var descriptor = error.IsLexer
                ? PlayscriptDiagnostics.UnexpectedToken
                : PlayscriptDiagnostics.MismatchedInput;
            diagnostics.Add(Diagnostic.Create(descriptor, MakeLocation(filePath, error.Line, error.Col), error.Msg));
        }
    }

    private static bool TryBuildContent(
        PlayscriptCodeBuilder builder,
        PlayscriptContentParser.ScriptContentContext tree,
        bool isScript,
        List<Diagnostic> diagnostics,
        string filePath,
        CancellationToken ct,
        out ScriptBlock? scriptBlock,
        out TextBlock? textBlock)
    {
        if (isScript)
        {
            builder.BuildScriptFromContent(tree);
            scriptBlock = builder.ContentResult;
            textBlock = null;
        }
        else
        {
            builder.BuildTextFromContent(tree);
            scriptBlock = null;
            textBlock = builder.TextResult;
        }

        AddContentDiagnostics(diagnostics, builder.Errors, filePath, ct);
        return builder.Errors.Count == 0;
    }
}
