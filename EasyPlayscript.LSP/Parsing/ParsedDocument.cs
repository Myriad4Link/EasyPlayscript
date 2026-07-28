using EasyPlayscript.LSP.Semantic;
using EasyPlayscript.Parsing;

namespace EasyPlayscript.LSP.Parsing;

/// <summary>
///     Cached tokens and errors for a single content block, used for incremental parsing.
///     When the block's raw content is unchanged between parses, this cache is reused
///     (with line offsets adjusted for any positional shift) instead of re-parsing.
/// </summary>
internal record CachedBlockContent(
    IReadOnlyList<TokenEntry> Tokens,
    IReadOnlyList<PlayscriptError> Errors);

/// <summary>
///     Result of parsing a <c>.scpt</c> file. Contains structure-level tokens/errors,
///     content-level tokens/errors merged and sorted, and an optional block cache
///     for incremental reparsing on document changes.
/// </summary>
internal class ParsedDocument(
    IReadOnlyList<TokenEntry> tokens,
    IReadOnlyList<PlayscriptError> errors,
    StructureParseResult structure,
    string? text = null,
    IReadOnlyDictionary<string, CachedBlockContent>? blockCache = null,
    IReadOnlyList<ValidationDiagnostic>? validationDiagnostics = null,
    PlayscriptCompilationData? compilationData = null)
{
    public IReadOnlyList<TokenEntry> Tokens { get; } = tokens;
    public IReadOnlyList<PlayscriptError> Errors { get; } = errors;
    public StructureParseResult Structure { get; } = structure;
    public string? Text { get; } = text;

    /// <summary>
    ///     Per-block cache keyed by block name. Enables incremental content parsing:
    ///     blocks whose <c>RawContent</c> is unchanged reuse cached tokens/errors
    ///     with only a line-offset adjustment. <c>null</c> on the first parse.
    /// </summary>
    public IReadOnlyDictionary<string, CachedBlockContent>? BlockCache { get; } = blockCache;

    /// <summary>
    ///     Per-file validation diagnostics produced by Pass 2 (full pipeline):
    ///     SCPT002/003 from content parsing, SCPT004 from duplicate script/text names.
    ///     Empty when the structure failed to parse.
    /// </summary>
    public IReadOnlyList<ValidationDiagnostic> ValidationDiagnostics { get; } =
        validationDiagnostics ?? [];

    /// <summary>
    ///     Per-file parsed data (scripts, texts, interfaces) produced by Pass 2.
    ///     Carries block locations and interface declarations for cross-file validation.
    ///     <c>null</c> when the structure failed to parse.
    /// </summary>
    public PlayscriptCompilationData? CompilationData { get; } = compilationData;
}
