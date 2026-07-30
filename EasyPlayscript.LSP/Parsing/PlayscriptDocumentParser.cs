using Antlr4.Runtime;
using EasyPlayscript.LSP.Mapping;
using EasyPlayscript.LSP.Semantic;
using EasyPlayscript.Parsing;

namespace EasyPlayscript.LSP.Parsing;

internal class PlayscriptDocumentParser
{
    public static ParsedDocument Parse(string content)
    {
        return ParseIncremental(content, null);
    }

    public static ParsedDocument ParseIncremental(string content, ParsedDocument? previous,
        string filePath = "")
    {
        var structureErrors = new List<PlayscriptError>();
        var structureTokens = CollectStructureTokens(content, structureErrors);
        var (structureResult, parseErrors) = PlayscriptStructureHelper.ParseStructureWithErrors(content);
        structureErrors.AddRange(parseErrors);

        // Pass 2 — run the full Core pipeline on the structure. Produces a per-file
        // PlayscriptCompilationData (scripts, texts, interfaces) and per-file
        // ValidationDiagnostics (SCPT002/003 from content errors, SCPT004 from
        // duplicate script/text names).
        var perFileData = new PlayscriptCompilationData();
        var validationDiagnostics = new List<ValidationDiagnostic>();
        validationDiagnostics.AddRange(PlayscriptPipeline.ProcessFile(
            structureResult, perFileData, filePath));

        var allTokens = new List<TokenEntry>(structureTokens);
        var contentErrors = new List<PlayscriptError>();
        var blockOffsets = ComputeBlockOffsets(content);
        var prevBlockIndex = BuildPrevBlockIndex(previous);
        var blockCache = new Dictionary<string, CachedBlockContent>();

        for (var i = 0; i < structureResult.Results.Count; i++)
        {
            var block = structureResult.Results[i];
            if (block.RawContent is null || i >= blockOffsets.Count) continue;

            var trimmed = block.RawContent.Trim('\r', '\n');
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (TryApplyCachedContent(block, blockOffsets[i], previous, prevBlockIndex,
                    allTokens, contentErrors, blockCache))
                continue;

            ReparseContent(block, trimmed, blockOffsets[i], allTokens, contentErrors, blockCache);
        }

        structureErrors.AddRange(contentErrors);
        allTokens.Sort((a, b) => a.Line != b.Line ? a.Line - b.Line : a.Col - b.Col);

        return new ParsedDocument(allTokens, structureErrors, structureResult, content, blockCache,
            validationDiagnostics, perFileData);
    }

    private static Dictionary<string, (StructureResult result, int index)>? BuildPrevBlockIndex(
        ParsedDocument? previous)
    {
        if (previous?.BlockCache is null) return null;

        var index = new Dictionary<string, (StructureResult, int)>();
        for (var i = 0; i < previous.Structure.Results.Count; i++)
        {
            var prev = previous.Structure.Results[i];
            index[prev.Name] = (prev, i);
        }

        return index;
    }

    private static bool TryApplyCachedContent(StructureResult block, BlockOffset offset,
        ParsedDocument? previous,
        Dictionary<string, (StructureResult result, int index)>? prevBlockIndex,
        List<TokenEntry> allTokens, List<PlayscriptError> contentErrors,
        Dictionary<string, CachedBlockContent> blockCache)
    {
        // A cache hit requires: (1) a previous parse exists with a block cache,
        // (2) the block existed before (same name), and (3) its raw content
        // is identical — meaning the user edited outside this block.
        if (previous?.BlockCache is null) return false;
        if (prevBlockIndex is null) return false;
        if (!prevBlockIndex.TryGetValue(block.Name, out var prevEntry)) return false;
        if (!previous.BlockCache.TryGetValue(block.Name, out var cached)) return false;
        if (prevEntry.result.RawContent != block.RawContent) return false;

        var lineDelta = block.Line - prevEntry.result.Line;

        var adjustedTokens = lineDelta == 0
            ? cached.Tokens
            : AdjustLineOffsets(cached.Tokens, lineDelta);

        var adjustedErrors = lineDelta == 0
            ? cached.Errors
            : AdjustErrorLineOffsets(cached.Errors, lineDelta);

        allTokens.AddRange(adjustedTokens);
        contentErrors.AddRange(adjustedErrors);
        blockCache[block.Name] = new CachedBlockContent(adjustedTokens, adjustedErrors);
        return true;
    }

    private static void ReparseContent(StructureResult block, string trimmedContent,
        BlockOffset offset, List<TokenEntry> allTokens, List<PlayscriptError> contentErrors,
        Dictionary<string, CachedBlockContent> blockCache)
    {
        var (contentTokens, errors) =
            CollectContentTokens(trimmedContent, offset, block.Identifier == BlockType.Script);
        allTokens.AddRange(contentTokens);
        contentErrors.AddRange(errors);
        blockCache[block.Name] = new CachedBlockContent(contentTokens, errors);
    }

    /// <summary>
    ///     Scans the structure token stream for <c>[RAW_CONTENT]</c> pairs and computes the
    ///     absolute line and column where each block's content begins. Leading newlines in
    ///     the raw content are accounted for so that content tokens map to the correct
    ///     document position.
    /// </summary>
    internal static List<BlockOffset> ComputeBlockOffsets(string content)
    {
        var inputStream = new AntlrInputStream(content);
        var lexer = new PlayscriptStructureLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        tokenStream.Fill();

        var offsets = new List<BlockOffset>();
        var allTokens = tokenStream.GetTokens();

        for (var i = 0; i < allTokens.Count - 1; i++)
        {
            if (allTokens[i].Type != PlayscriptStructureLexer.LBRACKET) continue;

            var rawToken = allTokens[i + 1];
            if (rawToken.Type != PlayscriptStructureLexer.RAW_CONTENT) continue;

            var rawText = rawToken.Text;
            var leadingNewlines = CountLeadingNewlines(rawText);

            offsets.Add(new BlockOffset(
                rawToken.Line + leadingNewlines,
                leadingNewlines > 0 ? 0 : rawToken.Column + leadingNewlines));
        }

        return offsets;
    }

    private static List<TokenEntry> AdjustLineOffsets(IReadOnlyList<TokenEntry> tokens, int delta)
    {
        var result = new List<TokenEntry>(tokens.Count);
        result.AddRange(tokens.Select(t =>
            t with { Line = t.Line + delta }));
        return result;
    }

    private static List<PlayscriptError> AdjustErrorLineOffsets(IReadOnlyList<PlayscriptError> errors,
        int delta)
    {
        var result = new List<PlayscriptError>(errors.Count);
        result.AddRange(errors.Select(e => new PlayscriptError(e.Line + delta, e.Col, e.Msg, e.IsLexer)));
        return result;
    }

    private static List<TokenEntry> CollectStructureTokens(string content, List<PlayscriptError> errors)
    {
        var inputStream = new AntlrInputStream(content);
        var lexer = new PlayscriptStructureLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);

        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(new StructureErrorCollector(errors));

        tokenStream.Fill();

        var result = new List<TokenEntry>();
        foreach (var token in tokenStream.GetTokens())
        {
            if (!TokenMapping.StructureTokenTypeMap.TryGetValue(token.Type, out var semanticType))
                continue;

            var line = PositionMapper.ToLspLine(token.Line);
            var col = token.Column;
            var length = token.Text.Length;

            var modifiers = 0;
            switch (token.Type)
            {
                case PlayscriptStructureLexer.INTERFACE:
                    modifiers |= SemanticTokenModifiers.Declaration;
                    break;
                case PlayscriptStructureLexer.ASYNC:
                    modifiers |= SemanticTokenModifiers.Async;
                    break;
            }

            result.Add(new TokenEntry(line, col, length, semanticType, modifiers));
        }

        return result;
    }

    private static (List<TokenEntry> tokens, List<PlayscriptError> errors) CollectContentTokens(
        string trimmedContent, in BlockOffset offset, bool isScript)
    {
        var inputStream = new AntlrInputStream(trimmedContent.Replace("\r\n", "\n"));
        var lexer = new PlayscriptContentLexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new PlayscriptContentParser(tokens)
        {
            BuildParseTree = false
        };

        var errors = new List<PlayscriptError>();
        lexer.RemoveErrorListeners();
        parser.RemoveErrorListeners();
        lexer.AddErrorListener(new ContentErrorCollector(errors));
        parser.AddErrorListener(new ContentErrorCollector(errors));

        if (isScript)
            parser.scriptContent();
        else
            parser.textContent();

        var result = new List<TokenEntry>();
        foreach (var token in tokens.GetTokens())
        {
            if (!TokenMapping.ContentTokenTypeMap.TryGetValue(token.Type, out var semanticType))
                continue;

            var line = PositionMapper.ToAbsoluteLine(token.Line, offset);
            // First line of content: add block's column offset
            // Subsequent lines: column is relative to line start (0)
            var col = token.Line == 1 ? token.Column + offset.ContentStartCol : token.Column;
            var length = token.Text.Length;

            result.Add(new TokenEntry(line, col, length, semanticType));
        }

        return (result, errors);
    }

    private static int CountLeadingNewlines(string text)
    {
        var count = 0;
        var i = 0;
        while (i < text.Length)
            if (text[i] == '\r')
            {
                count++;
                i++;
                if (i < text.Length && text[i] == '\n') i++;
            }
            else if (text[i] == '\n')
            {
                count++;
                i++;
            }
            else
            {
                break;
            }

        return count;
    }

    private class StructureErrorCollector(List<PlayscriptError> errors) : IAntlrErrorListener<int>
    {
        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            errors.Add(new PlayscriptError(line, charPositionInLine, msg, true));
        }
    }

    private class ContentErrorCollector(List<PlayscriptError> errors)
        : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e) =>
            errors.Add(new PlayscriptError(line, charPositionInLine, msg, true));

        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e) =>
            errors.Add(new PlayscriptError(line, charPositionInLine, msg, false));
    }
}
