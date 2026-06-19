using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                var blockType = blockTypeCtx.Start.Type switch
                {
                    PlayscriptStructureParser.SCRIPT => BlockType.Script,
                    PlayscriptStructureParser.TEXT => BlockType.Text,
                    _ => throw new System.InvalidOperationException("Unexpected blockType token"),
                };

                var nameNode = context.IDENTIFIER();
                if (nameNode == null) return string.Empty;

                var nameSymbol = nameNode.Symbol;
                var line = nameSymbol.Line;
                var col = nameSymbol.Column;

                var rawContent = context.RAW_CONTENT()?.GetText();

                Results.Add(new StructureResult(blockType, nameNode.GetText(), rawContent, line, col));
                return string.Empty;
            }

            if (context.INTERFACE() == null) return string.Empty;
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
                    parameters.AddRange(from paramCtx in paramListCtx.parameter()
                        let paramName = paramCtx.IDENTIFIER().GetText()
                        let paramType = MapTypeSpec(paramCtx.typeSpec())
                        select new InterfaceParameter(paramName, paramType));
                }

                var returnType = MapTypeSpec(typeSpecCtx);

                Interfaces.Add(new InterfaceDeclaration(nameNode.GetText(), parameters, returnType, line, col));
            }

            return string.Empty;
        }

        private static InterfaceType MapTypeSpec(PlayscriptStructureParser.TypeSpecContext typeSpec) =>
            typeSpec.Start.Type switch
            {
                PlayscriptStructureParser.STRING_TYPE => InterfaceType.String,
                PlayscriptStructureParser.INT_TYPE => InterfaceType.Int,
                PlayscriptStructureParser.DECIMAL_TYPE => InterfaceType.Decimal,
                PlayscriptStructureParser.BOOL_TYPE => InterfaceType.Bool,
                _ => InterfaceType.Void,
            };
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