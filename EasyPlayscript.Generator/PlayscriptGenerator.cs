using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using EasyPlayscript.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace EasyPlayscript.Generator;

[Generator]
public class PlayscriptGenerator : IIncrementalGenerator
{
    /// <summary>
    ///     Registers the incremental generator pipeline:
    ///     1. Parse each .scpt file into scripts, texts, and interfaces (Pass 1 + Pass 2).
    ///     2. Discover [Implementation]-decorated methods via ForAttributeWithMetadataName.
    ///     3. Merge per-file data, run cross-file validation, emit PlayscriptRegistry + PlayscriptRuntime.
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var scptProvider = context.AdditionalTextsProvider
            .Where(static f => f.Path.EndsWith(".scpt"))
            .Select(static (file, ct) => ParseSingleFile(file.Path,
                file.GetText(ct)?.ToString() ?? string.Empty, ct));

        var allFilesProvider = scptProvider.Collect();

        var implProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName("EasyPlayscript.ImplementationAttribute",
                static (node, _) => node is MethodDeclarationSyntax,
                static (ctx, ct) => ImplementationScanner.Extract(ctx, ct))
            .Collect();

        var combinedProvider = allFilesProvider
            .Combine(implProvider)
            .Combine(context.AnalyzerConfigOptionsProvider);

        context.RegisterSourceOutput(combinedProvider, static (spc, combined) =>
        {
            var ((allResults, implementations), configOptions)
                = combined;

            var ctx = new GeneratorContext(spc);

            foreach (var result in allResults)
            {
                foreach (var diag in result.Diagnostics)
                {
                    spc.CancellationToken.ThrowIfCancellationRequested();
                    spc.ReportDiagnostic(diag);
                    if (diag.Severity == DiagnosticSeverity.Error)
                        ctx.Data.HasErrors = true;
                }

                foreach (var diag in ctx.Data.MergeFrom(result.Data))
                {
                    spc.CancellationToken.ThrowIfCancellationRequested();
                    var descriptor = PlayscriptDiagnostics.GetDescriptor(diag.Code);
                    ctx.ReportDiagnostic(Diagnostic.Create(descriptor,
                        MakeLocation(diag.FilePath, diag.Line, diag.Col), diag.MessageArgs));
                }
            }

            foreach (var impl in Enumerable.OfType<ImplementationInfo>(implementations))
                ctx.Data.Implementations.Add(impl);

            foreach (var diag in PlayscriptPipeline.Validate(ctx.Data))
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                var descriptor = PlayscriptDiagnostics.GetDescriptor(diag.Code);
                ctx.ReportDiagnostic(Diagnostic.Create(descriptor,
                    MakeLocation(diag.FilePath, diag.Line, diag.Col), diag.MessageArgs));
            }

            if (ctx.Data.Implementations.Count > 0)
                foreach (var diag in PlayscriptPipeline.ValidateImplementations(ctx.Data))
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

            outputPath = string.IsNullOrEmpty(outputPath) ? "playscripts.bin" : outputPath;
            aesKey ??= string.Empty;

            var registryCode = PlayscriptRegistryEmitter.Generate(ctx.Data);
            spc.AddSource("PlayscriptRegistry.g.cs", SourceText.From(registryCode, Encoding.UTF8));

            var runtimeCode = PlayscriptRuntimeEmitter.Generate(
                ctx.Data.Scripts, ctx.Data.Texts, outputPath!, aesKey);
            spc.AddSource("PlayscriptRuntime.g.cs", SourceText.From(runtimeCode, Encoding.UTF8));
        });
    }

    /// <summary>
    ///     Parses a single .scpt file through the two-pass pipeline (structure → content).
    ///     Populates a <see cref="SingleFileResult" /> with parsed data and any diagnostics
    ///     from structure parsing, content parsing, or the processing pipeline.
    /// </summary>
    /// <param name="filePath">The source path of the .scpt file, used for diagnostic locations.</param>
    /// <param name="content">The raw text content of the .scpt file.</param>
    /// <param name="ct">Cancellation token, checked between pipeline stages.</param>
    /// <returns>
    ///     A <see cref="SingleFileResult" /> containing the parsed scripts, texts, and interfaces,
    ///     along with any diagnostics produced during parsing.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="ct" /> is cancelled.</exception>
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

    /// <summary>
    ///     Creates a Roslyn <see cref="Location" /> from 1-based line/col (as reported by ANTLR).
    /// </summary>
    /// <param name="filePath">The source file path.</param>
    /// <param name="line">1-based line number.</param>
    /// <param name="col">0-based column offset.</param>
    /// <returns>A zero-width <see cref="Location" /> at the specified position.</returns>
    private static Location MakeLocation(string filePath, int line, int col)
    {
        var linePosition = new LinePosition(line - 1, col);
        return Location.Create(filePath, default, new LinePositionSpan(linePosition, linePosition));
    }

    /// <summary>
    ///     Converts ANTLR parse errors (<see cref="PlayscriptError" />) into Roslyn diagnostics
    ///     and appends them to the target list. Maps lexer errors to SCPT002, parser errors to SCPT003.
    /// </summary>
    /// <param name="to">The accumulator list to append Roslyn diagnostics to.</param>
    /// <param name="errors">ANTLR parse errors from the content parser.</param>
    /// <param name="filePath">Source file path, used to construct <see cref="Location" /> for each error.</param>
    /// <param name="ct">Cancellation token, checked before each error conversion.</param>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="ct" /> is cancelled.</exception>
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

    /// <summary>
    ///     Mutable accumulator for the generator output phase. Tracks compilation data and
    ///     error state; reports diagnostics to the Roslyn <see cref="SourceProductionContext" />
    ///     and sets <see cref="PlayscriptCompilationData.HasErrors" /> on any error-level diagnostic.
    /// </summary>
    private sealed class GeneratorContext(SourceProductionContext spc)
    {
        public PlayscriptCompilationData Data { get; } = new();

        /// <summary>
        ///     Reports a diagnostic to the Roslyn output context and sets <see cref="Data" />.
        ///     <see cref="PlayscriptCompilationData.HasErrors" />
        ///     if the diagnostic severity is <see cref="DiagnosticSeverity.Error" />.
        /// </summary>
        /// <param name="diag">The Roslyn diagnostic to report.</param>
        public void ReportDiagnostic(Diagnostic diag)
        {
            spc.ReportDiagnostic(diag);
            if (diag.Severity == DiagnosticSeverity.Error)
                Data.HasErrors = true;
        }
    }

    /// <summary>
    ///     Per-file result from <see cref="ParseSingleFile" />. Contains the parsed data
    ///     (scripts, texts, interfaces) and any diagnostics produced during parsing.
    /// </summary>
    private sealed class SingleFileResult
    {
        public PlayscriptCompilationData Data { get; } = new();
        public List<Diagnostic> Diagnostics { get; } = [];
        public string FilePath { get; set; } = string.Empty;
    }
}