using EasyPlayscript.LSP.Mapping;
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
    IReadOnlyList<PlayscriptError> Errors,
    BlockOffset? Offset = null);

/// <summary>
///     Result of parsing a <c>.scpt</c> file. Contains structure-level tokens/errors,
///     content-level tokens/errors merged and sorted, and an optional block cache
///     for incremental re-parsing on document changes.
/// </summary>
internal class ParsedDocument(
    IReadOnlyList<TokenEntry> tokens,
    IReadOnlyList<PlayscriptError> errors,
    StructureParseResult structure,
    string? text = null,
    IReadOnlyDictionary<string, CachedBlockContent>? blockCache = null)
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
}