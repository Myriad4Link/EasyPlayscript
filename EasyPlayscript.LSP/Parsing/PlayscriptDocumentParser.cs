using Antlr4.Runtime;
using EasyPlayscript.LSP.Mapping;
using EasyPlayscript.LSP.Semantic;
using EasyPlayscript.Parsing;

namespace EasyPlayscript.LSP.Parsing;

internal class PlayscriptDocumentParser
{
    // TODO: add errors publishing other than syntax and lexer ones.
    public static ParsedDocument Parse(string content)
    {
        var structureErrors = new List<PlayscriptError>();
        var structureTokens = CollectStructureTokens(content, structureErrors);
        var (structureResult, parseErrors) = PlayscriptStructureHelper.ParseStructureWithErrors(content);
        structureErrors.AddRange(parseErrors);

        var allTokens = new List<TokenEntry>(structureTokens);
        var contentErrors = new List<PlayscriptError>();

        var blockOffsets = ComputeBlockOffsets(content);

        for (var i = 0; i < structureResult.Results.Count; i++)
        {
            var block = structureResult.Results[i];
            if (block.RawContent is null || i >= blockOffsets.Count) continue;

            var trimmed = block.RawContent.Trim('\r', '\n');
            if (string.IsNullOrEmpty(trimmed)) continue;

            var offset = blockOffsets[i];
            var (contentTokens, errors) = CollectContentTokens(trimmed, offset, block.Identifier == BlockType.Script);
            allTokens.AddRange(contentTokens);
            contentErrors.AddRange(errors);
        }

        structureErrors.AddRange(contentErrors);
        allTokens.Sort((a, b) => a.Line != b.Line ? a.Line - b.Line : a.Col - b.Col);

        return new ParsedDocument(allTokens, structureErrors, structureResult);
    }

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
        var inputStream = new AntlrInputStream(trimmedContent);
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
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            errors.Add(new PlayscriptError(line, charPositionInLine, msg, true));
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            errors.Add(new PlayscriptError(line, charPositionInLine, msg, false));
        }
    }
}