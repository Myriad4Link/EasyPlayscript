using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using EasyPlayscript.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace EasyPlayscript;

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

        var implProvider = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => IsMethodWithImplementationAttribute(node),
            static (ctx, ct) => ImplementationScanner.Extract(ctx, ct))
            .Where(static impl => impl is not null)
            .Collect();

        var combinedProvider = allFilesProvider
            .Combine(implProvider)
            .Combine(context.AnalyzerConfigOptionsProvider);

        context.RegisterSourceOutput(combinedProvider, static (spc, combined) =>
        {
            var ((allResults, implementations), configOptions) = combined;

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

            foreach (var impl in Enumerable.OfType<ImplementationInfo>(implementations))
            {
                ctx.Data.Implementations.Add(impl);
            }

            foreach (var diag in PlayscriptPipeline.Validate(ctx.Data))
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                var descriptor = PlayscriptDiagnostics.GetDescriptor(diag.Code);
                ctx.ReportDiagnostic(Diagnostic.Create(descriptor,
                    MakeLocation(diag.FilePath, diag.Line, diag.Col), diag.MessageArgs));
            }

            if (ctx.Data.Implementations.Count > 0)
            {
                foreach (var diag in PlayscriptPipeline.ValidateImplementations(ctx.Data))
                {
                    spc.CancellationToken.ThrowIfCancellationRequested();
                    var descriptor = PlayscriptDiagnostics.GetDescriptor(diag.Code);
                    ctx.ReportDiagnostic(Diagnostic.Create(descriptor,
                        MakeLocation(diag.FilePath, diag.Line, diag.Col), diag.MessageArgs));
                }
            }

            if (ctx.Data.HasErrors)
                return;

            spc.CancellationToken.ThrowIfCancellationRequested();

            configOptions.GlobalOptions.TryGetValue("build_property.PlayscriptOutputPath", out var outputPath);
            configOptions.GlobalOptions.TryGetValue("build_property.PlayscriptAesKey", out var aesKey);

            outputPath = string.IsNullOrEmpty(outputPath) ? "playscripts.bin" : outputPath;
            aesKey ??= string.Empty;

            var registryCode = PlayscriptRegistryEmitter.Generate(ctx.Data);
            spc.AddSource("PlayscriptRegistry.g.cs", SourceText.From(registryCode, Encoding.UTF8));

            var contextCode = PlayscriptContextEmitter.Generate(
                ctx.Data.Scripts, ctx.Data.Texts, outputPath!, aesKey);
            spc.AddSource("PlayscriptContext.g.cs", SourceText.From(contextCode, Encoding.UTF8));
        });
    }

    private sealed class GeneratorContext(SourceProductionContext spc)
    {
        public PlayscriptCompilationData Data { get; } = new();

        public void ReportDiagnostic(Diagnostic diag)
        {
            spc.ReportDiagnostic(diag);
            if (diag.Severity == DiagnosticSeverity.Error)
                Data.HasErrors = true;
        }
    }

    private static bool IsMethodWithImplementationAttribute(SyntaxNode node)
    {
        if (node is not MethodDeclarationSyntax method)
            return false;

        return method.AttributeLists.Any(list =>
            list.Attributes.Any(attr =>
                attr.Name.ToString() is "Implementation" or "ImplementationAttribute"));
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
