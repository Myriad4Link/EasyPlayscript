using EasyPlayscript.Parsing;

namespace EasyPlayscript.LSP.Semantic;

internal static class SemanticTokenTypes
{
    public const int Keyword = 0;
    public const int Modifier = 1;
    public const int Type = 2;
    public const int Function = 3;
    public const int String = 4;
    public const int Number = 5;
    public const int Comment = 6;
    public const int Operator = 7;
    public const int Variable = 8;
    public const int Boolean = 9;
}

internal static class SemanticTokenModifiers
{
    public const int Declaration = 1 << 0;
    public const int Async = 1 << 1;
}

internal static class TokenMapping
{
    public static readonly Dictionary<int, int> StructureTokenTypeMap = new()
    {
        [PlayscriptStructureLexer.SCRIPT] = SemanticTokenTypes.Keyword,
        [PlayscriptStructureLexer.TEXT] = SemanticTokenTypes.Keyword,
        [PlayscriptStructureLexer.ASYNC] = SemanticTokenTypes.Modifier,
        [PlayscriptStructureLexer.INTERFACE] = SemanticTokenTypes.Keyword,
        [PlayscriptStructureLexer.STRING_TYPE] = SemanticTokenTypes.Type,
        [PlayscriptStructureLexer.INT_TYPE] = SemanticTokenTypes.Type,
        [PlayscriptStructureLexer.DECIMAL_TYPE] = SemanticTokenTypes.Type,
        [PlayscriptStructureLexer.BOOL_TYPE] = SemanticTokenTypes.Type,
        [PlayscriptStructureLexer.VOID_TYPE] = SemanticTokenTypes.Type,
        [PlayscriptStructureLexer.IDENTIFIER] = SemanticTokenTypes.Variable,
        [PlayscriptStructureLexer.COMMENT] = SemanticTokenTypes.Comment,
        [PlayscriptStructureLexer.COLON] = SemanticTokenTypes.Operator,
        [PlayscriptStructureLexer.LPAREN] = SemanticTokenTypes.Operator,
        [PlayscriptStructureLexer.RPAREN] = SemanticTokenTypes.Operator,
        [PlayscriptStructureLexer.COMMA] = SemanticTokenTypes.Operator,
        [PlayscriptStructureLexer.LBRACKET] = SemanticTokenTypes.Operator,
        [PlayscriptStructureLexer.RBRACKET] = SemanticTokenTypes.Operator
    };

    public static readonly Dictionary<int, int> ContentTokenTypeMap = new()
    {
        [PlayscriptContentLexer.AT] = SemanticTokenTypes.Operator,
        [PlayscriptContentLexer.COMMENT] = SemanticTokenTypes.Comment,
        [PlayscriptContentLexer.LPAREN] = SemanticTokenTypes.Operator,
        [PlayscriptContentLexer.RPAREN] = SemanticTokenTypes.Operator,
        [PlayscriptContentLexer.STRING_LITERAL] = SemanticTokenTypes.String,
        [PlayscriptContentLexer.IDENTIFIER] = SemanticTokenTypes.Function,
        [PlayscriptContentLexer.COMMA] = SemanticTokenTypes.Operator,
        [PlayscriptContentLexer.INTEGER_LITERAL] = SemanticTokenTypes.Number,
        [PlayscriptContentLexer.FLOAT_LITERAL] = SemanticTokenTypes.Number,
        [PlayscriptContentLexer.BOOLEAN_LITERAL] = SemanticTokenTypes.Boolean,
        [PlayscriptContentLexer.SLASH] = SemanticTokenTypes.Operator,
        [PlayscriptContentLexer.PLUS] = SemanticTokenTypes.Operator,
        [PlayscriptContentLexer.TEXT] = SemanticTokenTypes.String
    };
}