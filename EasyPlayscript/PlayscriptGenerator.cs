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
            .Select(static (file, ct) => ParseSingleFile(file.Path,
                file.GetText(ct)?.ToString() ?? string.Empty, ct));


        var allFilesProvider = scptProvider.Collect();
        var combinedProvider = context.AnalyzerConfigOptionsProvider.Combine(allFilesProvider);

        context.RegisterSourceOutput(combinedProvider, static (spc, combined) =>
        {
            var (configOptions, allResults) = combined;

            var ctx = new GeneratorContext(spc);

            foreach (var result in allResults)
                MergeFileData(ctx.Data, result);

            foreach (var diag in allResults.SelectMany(result => result.Diagnostics))
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                spc.ReportDiagnostic(diag);
                if (diag.Severity == DiagnosticSeverity.Error)
                    ctx.Data.HasErrors = true;
            }

            foreach (var diag in PlayscriptPipeline.Validate(ctx.Data))
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                var descriptor = PlayscriptDiagnostics.GetDescriptor(diag.Code);
                ctx.ReportDiagnostic(Diagnostic.Create(descriptor,
                    MakeLocation(diag.FilePath, diag.Line, diag.Col), diag.MessageArgs));
            }

            if (ctx.Data.HasErrors)
                return;

            spc.CancellationToken.ThrowIfCancellationRequested();

            configOptions.GlobalOptions.TryGetValue("build_property.PlayscriptOutputPath", out var outputPath);
            configOptions.GlobalOptions.TryGetValue("build_property.PlayscriptAesKey", out var aesKey);
            configOptions.GlobalOptions.TryGetValue("build_property.PlayscriptBaseClass", out var className);

            outputPath = string.IsNullOrEmpty(outputPath) ? "playscripts.bin" : outputPath;
            aesKey ??= string.Empty;
            className = string.IsNullOrEmpty(className) ? "PlayscriptBase" : className;

            var code = PlayscriptBaseEmitter.Generate(ctx.Data.Scripts, ctx.Data.Texts,
                ctx.Data.Interfaces, outputPath!, aesKey!, className!);

            spc.CancellationToken.ThrowIfCancellationRequested();
            spc.AddSource($"{className}.g.cs", SourceText.From(code, Encoding.UTF8));
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

    private static void MergeFileData(PlayscriptCompilationData data, SingleFileResult result)
    {
        foreach (var kvp in result.Data.Scripts)
        {
            if (data.Scripts.ContainsKey(kvp.Key))
            {
                var loc = data.ScriptLocations[kvp.Key];
                result.Diagnostics.Add(Diagnostic.Create(
                    PlayscriptDiagnostics.DuplicateScriptName,
                    MakeLocation(loc.filePath, loc.line, loc.col),
                    "script", kvp.Key));
            }
            else
            {
                data.ScriptLocations[kvp.Key] = result.Data.ScriptLocations[kvp.Key];
                data.Scripts[kvp.Key] = kvp.Value;
            }
        }

        foreach (var kvp in result.Data.Texts)
        {
            if (data.Texts.ContainsKey(kvp.Key))
            {
                var loc = data.TextLocations[kvp.Key];
                result.Diagnostics.Add(Diagnostic.Create(
                    PlayscriptDiagnostics.DuplicateScriptName,
                    MakeLocation(loc.filePath, loc.line, loc.col),
                    "text", kvp.Key));
            }
            else
            {
                data.TextLocations[kvp.Key] = result.Data.TextLocations[kvp.Key];
                data.Texts[kvp.Key] = kvp.Value;
            }
        }

        data.Interfaces.AddRange(result.Data.Interfaces);
    }

    private sealed class SingleFileResult
    {
        public PlayscriptCompilationData Data { get; } = new();
        public List<Diagnostic> Diagnostics { get; } = [];
        public string FilePath { get; set; } = string.Empty;
    }

    private static SingleFileResult ParseSingleFile(string filePath, string content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var (structureResult, structureErrors) = PlayscriptStructureHelper.ParseStructureWithErrors(content);

        var result = new SingleFileResult { FilePath = filePath };

        AppendContentDiagnostics(result.Diagnostics, structureErrors, filePath, ct);

        var pipelineDiagnostics = PlayscriptPipeline.ProcessFile(
            structureResult, result.Data, filePath, ct);

        foreach (var diag in pipelineDiagnostics)
        {
            var descriptor = PlayscriptDiagnostics.GetDescriptor(diag.Code);
            result.Diagnostics.Add(Diagnostic.Create(descriptor,
                MakeLocation(diag.FilePath, diag.Line, diag.Col), diag.MessageArgs));
        }

        return result;
    }

    private static Location MakeLocation(string filePath, int line, int col)
    {
        var linePosition = new LinePosition(line - 1, col);
        return Location.Create(filePath, default, new LinePositionSpan(linePosition, linePosition));
    }

    private static void AppendContentDiagnostics(
        List<Diagnostic> to,
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
            to.Add(Diagnostic.Create(descriptor, MakeLocation(filePath, error.Line, error.Col), error.Msg));
        }
    }
}