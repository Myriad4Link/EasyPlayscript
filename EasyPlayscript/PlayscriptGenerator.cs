using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Antlr4.Runtime.Tree;
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

            foreach (var diag in allResults.SelectMany(result => result.Diagnostics))
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                spc.ReportDiagnostic(diag);
                if (diag.Severity == DiagnosticSeverity.Error)
                    ctx.Data.HasErrors = true;
            }

            ctx.MergeBlocks(allResults);
            ctx.ReportValidationDiagnostics();

            if (ctx.Data.HasErrors)
                return;

            spc.CancellationToken.ThrowIfCancellationRequested();

            configOptions.GlobalOptions.TryGetValue("build_property.PlayscriptOutputPath", out var outputPath);
            configOptions.GlobalOptions.TryGetValue("build_property.PlayscriptAesKey", out var aesKey);

            outputPath = string.IsNullOrEmpty(outputPath) ? "playscripts.bin" : outputPath;
            aesKey ??= string.Empty;

            var code = RegistryEmitter.Generate(ctx.Data.Scripts, ctx.Data.Texts, outputPath!, aesKey!);

            spc.CancellationToken.ThrowIfCancellationRequested();
            spc.AddSource("Registry.g.cs", SourceText.From(code, Encoding.UTF8));
        });
    }

    private sealed class GeneratorContext(SourceProductionContext spc)
    {
        public PlayscriptCompilationData Data { get; } = new();

        private void ReportDiagnostic(Diagnostic diag)
        {
            spc.ReportDiagnostic(diag);
            Data.HasErrors = true;
        }

        public void MergeBlocks(ImmutableArray<SingleFileResult> allResults)
        {
            foreach (var result in allResults)
            {
                Data.Interfaces.AddRange(result.Interfaces);

                foreach (var (name, block, line, col) in result.ScriptBlocks)
                {
                    if (Data.Scripts.ContainsKey(name))
                    {
                        var loc = Data.ScriptLocations[name];
                        ReportDiagnostic(Diagnostic.Create(PlayscriptDiagnostics.DuplicateScriptName,
                            MakeLocation(loc.filePath, loc.line, loc.col),
                            "script", name));
                    }
                    else
                    {
                        Data.ScriptLocations[name] = (result.FilePath, line, col);
                        Data.Scripts[name] = block;
                    }
                }

                foreach (var (name, block, line, col) in result.TextBlocks)
                {
                    if (Data.Texts.ContainsKey(name))
                    {
                        var loc = Data.TextLocations[name];
                        ReportDiagnostic(Diagnostic.Create(PlayscriptDiagnostics.DuplicateScriptName,
                            MakeLocation(loc.filePath, loc.line, loc.col),
                            "text", name));
                    }
                    else
                    {
                        Data.TextLocations[name] = (result.FilePath, line, col);
                        Data.Texts[name] = block;
                    }
                }
            }
        }

        public void ReportValidationDiagnostics()
        {
            var allDiagnostics = new List<ValidationDiagnostic>();
            allDiagnostics.AddRange(InterfaceValidator.ValidateUndeclaredCalls(Data));
            allDiagnostics.AddRange(InterfaceValidator.ValidateDuplicateSignatures(Data));
            allDiagnostics.AddRange(InterfaceValidator.ValidateArgumentTypes(Data));

            foreach (var diag in allDiagnostics)
            {
                var descriptor = PlayscriptDiagnostics.GetDescriptor(diag.Code);
                ReportDiagnostic(Diagnostic.Create(descriptor,
                    MakeLocation(diag.FilePath, diag.Line, diag.Col), diag.MessageArgs));
            }
        }
    }

    private sealed class SingleFileResult
    {
        public List<(string name, ScriptBlock block, int line, int col)> ScriptBlocks { get; } = [];
        public List<(string name, TextBlock block, int line, int col)> TextBlocks { get; } = [];
        public List<InterfaceDeclaration> Interfaces { get; } = [];
        public List<Diagnostic> Diagnostics { get; } = [];
        public string FilePath { get; set; } = string.Empty;
    }

    private static SingleFileResult ParseSingleFile(string filePath, string content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var structureResults = PlayscriptStructureHelper.ParseStructureWithErrors(content);

        var result = new SingleFileResult { FilePath = filePath };

        AppendContentDiagnostics(result.Diagnostics, structureResults.errors, filePath, ct);

        foreach (var (identifier, name, rawContent, line, col) in structureResults.result.Results)
        {
            ct.ThrowIfCancellationRequested();
            if (rawContent == null) continue;

            var trimmedContent = rawContent.Trim('\r', '\n');

            var (parser, contentErrors) = identifier == BlockType.Script
                ? PlayscriptContentHelper.ParseScript(trimmedContent)
                : PlayscriptContentHelper.ParseText(trimmedContent);

            IParseTree? tree = identifier == BlockType.Script
                ? parser.scriptContent()
                : parser.textContent();

            AppendContentDiagnostics(to: result.Diagnostics, contentErrors, filePath, ct);
            if (contentErrors.Count > 0) continue;

            ct.ThrowIfCancellationRequested();
            var builder = new PlayscriptCodeBuilder(ct);

            builder.Build(identifier, tree);
            AppendContentDiagnostics(result.Diagnostics, builder.Errors, filePath, ct);
            if (builder.Errors.Count > 0) continue;

            if (identifier == BlockType.Script)
                result.ScriptBlocks.Add((name, builder.ContentResult, line, col));
            else
                result.TextBlocks.Add((name, builder.TextResult, line, col));
        }

        foreach (var i in structureResults.result.Interfaces)
            i.FilePath = filePath;
        result.Interfaces.AddRange(structureResults.result.Interfaces);

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