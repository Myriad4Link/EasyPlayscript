using EasyPlayscript.LSP.Semantic;
using EasyPlayscript.Parsing;

namespace EasyPlayscript.LSP.Parsing;

internal class ParsedDocument(
    IReadOnlyList<TokenEntry> tokens,
    IReadOnlyList<PlayscriptError> errors,
    StructureParseResult structure)
{
    public IReadOnlyList<TokenEntry> Tokens { get; } = tokens;
    public IReadOnlyList<PlayscriptError> Errors { get; } = errors;
    public StructureParseResult Structure { get; } = structure;
}