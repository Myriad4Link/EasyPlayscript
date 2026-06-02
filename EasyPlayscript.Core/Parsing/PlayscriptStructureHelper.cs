using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;

namespace EasyPlayscript.Parsing;

/// <summary>
/// Helper for Pass 1 structure recognition. Parses playscript source and extracts
/// compiler calls with their associated script block raw content.
/// </summary>
public static class PlayscriptStructureHelper
{
    public static StructureParseResult ParseStructure(string content)
    {
        var (result, _) = ParseStructureWithErrors(content);
        return result;
    }

    public static (StructureParseResult result, List<PlayscriptError> errors)
        ParseStructureWithErrors(string content)
    {
        var inputStream = new AntlrInputStream(content);
        var lexer = new PlayscriptStructureLexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new PlayscriptStructureParser(tokens);

        var errors = new List<PlayscriptError>();
        lexer.RemoveErrorListeners();
        parser.RemoveErrorListeners();
        lexer.AddErrorListener(new CollectingErrorListener(errors, isLexer: true));
        parser.AddErrorListener(new CollectingErrorListener(errors, isLexer: false));

        var tree = parser.playscript();
        var visitor = new StructureVisitor();
        visitor.Visit(tree);

        return (new StructureParseResult(visitor.Results, visitor.Interfaces), errors);
    }

    private class StructureVisitor : PlayscriptStructureParserBaseVisitor<string>
    {
        public List<StructureResult> Results { get; } = [];
        public List<InterfaceDeclaration> Interfaces { get; } = [];

        public override string VisitTopLevelStatement(PlayscriptStructureParser.TopLevelStatementContext context)
        {
            var blockTypeCtx = context.blockType();
            if (blockTypeCtx != null)
            {
                BlockType blockType;
                if (blockTypeCtx.SCRIPT() != null)
                    blockType = BlockType.Script;
                else if (blockTypeCtx.TEXT() != null)
                    blockType = BlockType.Text;
                else
                    return string.Empty;

                var nameNode = context.IDENTIFIER();
                if (nameNode == null) return string.Empty;

                var nameSymbol = nameNode.Symbol;
                var line = nameSymbol.Line;
                var col = nameSymbol.Column;

                string rawContent = null;
                if (context.RAW_CONTENT() != null) rawContent = context.RAW_CONTENT().GetText();

                Results.Add(new StructureResult(blockType, nameNode.GetText(), rawContent, line, col));
                return string.Empty;
            }

            if (context.INTERFACE() != null)
            {
                var nameNode = context.IDENTIFIER();
                if (nameNode == null) return string.Empty;

                var typeSpecCtx = context.typeSpec();
                if (typeSpecCtx == null) return string.Empty;

                var nameSymbol = nameNode.Symbol;
                var line = nameSymbol.Line;
                var col = nameSymbol.Column;

                var parameters = new List<InterfaceParameter>();
                var paramListCtx = context.paramList();
                if (paramListCtx != null)
                {
                    foreach (var paramCtx in paramListCtx.parameter())
                    {
                        var paramName = paramCtx.IDENTIFIER().GetText();
                        var paramType = MapTypeSpec(paramCtx.typeSpec());
                        parameters.Add(new InterfaceParameter(paramName, paramType));
                    }
                }

                var returnType = MapTypeSpec(typeSpecCtx);

                Interfaces.Add(new InterfaceDeclaration(nameNode.GetText(), parameters, returnType, line, col));
            }

            return string.Empty;
        }

        private static InterfaceType MapTypeSpec(PlayscriptStructureParser.TypeSpecContext typeSpec)
        {
            if (typeSpec.STRING_TYPE() != null) return InterfaceType.String;
            if (typeSpec.INT_TYPE() != null) return InterfaceType.Int;
            if (typeSpec.DECIMAL_TYPE() != null) return InterfaceType.Decimal;
            if (typeSpec.BOOL_TYPE() != null) return InterfaceType.Bool;
            return InterfaceType.Void;
        }
    }

    private class CollectingErrorListener(List<PlayscriptError> errors, bool isLexer)
        : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e) =>
            errors.Add(new PlayscriptError(line, charPositionInLine, msg, isLexer));

        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e) =>
            errors.Add(new PlayscriptError(line, charPositionInLine, msg, isLexer));
    }
}
