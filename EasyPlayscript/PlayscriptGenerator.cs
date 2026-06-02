using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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

            var ctx = new MergeContext(spc);

            foreach (var diag in allResults.SelectMany(result => result.diagnostics))
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                spc.ReportDiagnostic(diag);
                if (diag.Severity == DiagnosticSeverity.Error)
                    ctx.HasErrors = true;
            }

            MergeBlocks(allResults, ctx);
            ReportValidationDiagnostics(ctx);

            if (ctx.HasErrors)
                return;

            spc.CancellationToken.ThrowIfCancellationRequested();

            configOptions.GlobalOptions.TryGetValue("build_property.PlayscriptOutputPath", out var outputPath);
            configOptions.GlobalOptions.TryGetValue("build_property.PlayscriptAesKey", out var aesKey);

            outputPath = string.IsNullOrEmpty(outputPath) ? "playscripts.bin" : outputPath;
            aesKey = string.IsNullOrEmpty(aesKey) ? "dev-key-change-me" : aesKey;

            var code = GenerateRegistryClass(ctx.Scripts, ctx.Texts, outputPath!, aesKey!);

            spc.CancellationToken.ThrowIfCancellationRequested();
            spc.AddSource("Registry.g.cs", SourceText.From(code, Encoding.UTF8));
        });
    }

    private sealed class MergeContext
    {
        public Dictionary<string, ScriptBlock> Scripts { get; } = new();
        public Dictionary<string, ScriptBlock> Texts { get; } = new();
        public Dictionary<string, (string filePath, int line, int col)> ScriptLocations { get; } = new();
        public Dictionary<string, (string filePath, int line, int col)> TextLocations { get; } = new();
        public List<InterfaceDeclaration> Interfaces { get; } = new();
        public bool HasErrors { get; set; }

        private readonly SourceProductionContext _spc;

        public MergeContext(SourceProductionContext spc) => _spc = spc;

        public void ReportDiagnostic(Diagnostic diag)
        {
            _spc.ReportDiagnostic(diag);
            HasErrors = true;
        }
    }

    private static (
        List<(BlockType identifier, string name, ScriptBlock block, int line, int col)> blocks,
        List<InterfaceDeclaration> interfaces,
        string filePath,
        List<Diagnostic> diagnostics)
        ParseSingleFile(string filePath, string content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var structureResults = PlayscriptStructureHelper.ParseStructureWithErrors(content);

        var diagnostics = new List<Diagnostic>();
        var blocks = new List<(BlockType identifier, string name, ScriptBlock block, int line, int col)>();

        foreach (var (line, col, msg, isLexer) in structureResults.errors)
        {
            ct.ThrowIfCancellationRequested();
            var descriptor = isLexer
                ? PlayscriptDiagnostics.UnexpectedToken
                : PlayscriptDiagnostics.MismatchedInput;
            diagnostics.Add(Diagnostic.Create(descriptor, MakeLocation(filePath, line, col), msg));
        }

        foreach (var (identifier, name, rawContent, line, col) in structureResults.result.Results)
        {
            ct.ThrowIfCancellationRequested();
            if (rawContent == null) continue;

            var trimmedContent = rawContent.Trim('\r', '\n');
            var (parser, contentErrors) = PlayscriptContentHelper.Parse(trimmedContent);
            var tree = parser.scriptContent();

            foreach (var error in contentErrors)
            {
                ct.ThrowIfCancellationRequested();
                var descriptor = error.IsLexer
                    ? PlayscriptDiagnostics.UnexpectedToken
                    : PlayscriptDiagnostics.MismatchedInput;
                diagnostics.Add(Diagnostic.Create(descriptor, MakeLocation(filePath, error.Line, error.Col), error.Msg));
            }

            if (contentErrors.Count > 0) continue;

            ct.ThrowIfCancellationRequested();
            var builder = new PlayscriptCodeBuilder(ct);
            builder.BuildFromContent(tree);

            foreach (var error in builder.Errors)
            {
                ct.ThrowIfCancellationRequested();
                var descriptor = error.IsLexer
                    ? PlayscriptDiagnostics.UnexpectedToken
                    : PlayscriptDiagnostics.MismatchedInput;
                diagnostics.Add(Diagnostic.Create(descriptor, MakeLocation(filePath, error.Line, error.Col), error.Msg));
            }

            if (builder.Errors.Count > 0) continue;

            blocks.Add((identifier, name, builder.ContentResult, line, col));
        }

        var interfaces = structureResults.result.Interfaces;
        foreach (var i in interfaces)
            i.FilePath = filePath;

        return (blocks, interfaces, filePath, diagnostics);
    }

    private static void MergeBlocks(
        ImmutableArray<(List<(BlockType identifier, string name, ScriptBlock block, int line, int col)> blocks,
            List<InterfaceDeclaration> interfaces, string filePath, List<Diagnostic> diagnostics)> allResults,
        MergeContext ctx)
    {
        foreach (var result in allResults)
        {
            ctx.Interfaces.AddRange(result.interfaces);

            foreach (var (identifier, name, block, line, col) in result.blocks)
            {
                switch (identifier)
                {
                    case BlockType.Script:
                    {
                        if (ctx.Scripts.ContainsKey(name))
                        {
                            var loc = ctx.ScriptLocations[name];
                            ctx.ReportDiagnostic(Diagnostic.Create(PlayscriptDiagnostics.DuplicateScriptName,
                                MakeLocation(loc.filePath, loc.line, loc.col),
                                "script", name));
                        }
                        else
                            ctx.ScriptLocations[name] = (result.filePath, line, col);

                        ctx.Scripts[name] = block;
                        break;
                    }
                    case BlockType.Text:
                    {
                        if (ctx.Texts.ContainsKey(name))
                        {
                            var loc = ctx.TextLocations[name];
                            ctx.ReportDiagnostic(Diagnostic.Create(PlayscriptDiagnostics.DuplicateScriptName,
                                MakeLocation(loc.filePath, loc.line, loc.col),
                                "text", name));
                        }
                        else
                            ctx.TextLocations[name] = (result.filePath, line, col);

                        ctx.Texts[name] = block;
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }

    private static void ReportValidationDiagnostics(MergeContext ctx)
    {
        var allDiagnostics = new List<ValidationDiagnostic>();
        allDiagnostics.AddRange(InterfaceValidator.ValidateUndeclaredCalls(
            ctx.Interfaces, ctx.Scripts, ctx.ScriptLocations, ctx.Texts, ctx.TextLocations));
        allDiagnostics.AddRange(InterfaceValidator.ValidateDuplicateSignatures(ctx.Interfaces));
        allDiagnostics.AddRange(InterfaceValidator.ValidateArgumentTypes(
            ctx.Interfaces, ctx.Scripts, ctx.ScriptLocations, ctx.Texts, ctx.TextLocations));

        foreach (var diag in allDiagnostics)
        {
            var descriptor = diag.Code switch
            {
                "SCPT005" => PlayscriptDiagnostics.UndeclaredConsumerCall,
                "SCPT006" => PlayscriptDiagnostics.DuplicateInterfaceSignature,
                "SCPT007" => PlayscriptDiagnostics.ArgumentTypeMismatch,
                "SCPT008" => PlayscriptDiagnostics.ArgumentCountMismatch,
                _ => null
            };
            if (descriptor != null)
                ctx.ReportDiagnostic(Diagnostic.Create(descriptor,
                    MakeLocation(diag.FilePath, diag.Line, diag.Col), diag.MessageArgs));
        }
    }

    private static Location MakeLocation(string filePath, int line, int col)
    {
        var linePosition = new LinePosition(line - 1, col);
        return Location.Create(filePath, default, new LinePositionSpan(linePosition, linePosition));
    }

    private static string ToScreamingSnakeCase(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name)
        {
            sb.Append(c is ' ' or '-' ? '_' : char.ToUpperInvariant(c));
        }

        return sb.ToString();
    }

    private static string GenerateRegistryClass(Dictionary<string, ScriptBlock> scripts,
        Dictionary<string, ScriptBlock> texts, string outputPath, string aesKey)
    {
        using var writer = new StringWriter();
        var indented = new IndentedTextWriter(writer);

        indented.WriteLine("// <auto-generated/>");
        indented.WriteLine();
        indented.WriteLine("using EasyPlayscript;");
        indented.WriteLine("using EasyPlayscript.Generated;");
        indented.WriteLine();
        indented.WriteLine("public static class Registry");
        indented.WriteLine("{");
        indented.Indent++;

        indented.WriteLine(
            $"private static readonly System.Lazy<System.Collections.Generic.Dictionary<string, ScriptBlock>> _scripts =");
        indented.Indent++;
        indented.WriteLine($"new(() => PlayscriptLoader.LoadScripts(\"{outputPath}\", \"{aesKey}\"));");
        indented.Indent--;
        indented.WriteLine();
        indented.WriteLine(
            $"private static readonly System.Lazy<System.Collections.Generic.Dictionary<string, ScriptBlock>> _texts =");
        indented.Indent++;
        indented.WriteLine($"new(() => PlayscriptLoader.LoadTexts(\"{outputPath}\", \"{aesKey}\"));");
        indented.Indent--;
        indented.WriteLine();

        var sortedScripts = scripts.OrderBy(kvp => kvp.Key, System.StringComparer.Ordinal);
        foreach (var kvp in sortedScripts)
        {
            var propName = ToScreamingSnakeCase(kvp.Key);
            indented.WriteLine(
                $"public static Script {propName} => new Script {{ Block = _scripts.Value[\"{kvp.Key}\"] }};");
        }

        var sortedTexts = texts.OrderBy(kvp => kvp.Key, System.StringComparer.Ordinal);
        foreach (var kvp in sortedTexts)
        {
            var propName = ToScreamingSnakeCase(kvp.Key);
            indented.WriteLine($"public static Text {propName} => new Text {{ Block = _texts.Value[\"{kvp.Key}\"] }};");
        }

        indented.Indent--;
        indented.WriteLine("}");

        indented.Flush();
        return writer.ToString();
    }
}
