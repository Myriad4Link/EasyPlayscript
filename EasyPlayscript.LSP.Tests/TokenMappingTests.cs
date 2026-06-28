using EasyPlayscript.LSP.Semantic;
using EasyPlayscript.Parsing;

namespace EasyPlayscript.LSP.Tests;

public class TokenMappingTests
{
    [Theory]
    [InlineData(PlayscriptStructureLexer.SCRIPT, SemanticTokenTypes.Keyword)]
    [InlineData(PlayscriptStructureLexer.TEXT, SemanticTokenTypes.Keyword)]
    [InlineData(PlayscriptStructureLexer.INTERFACE, SemanticTokenTypes.Keyword)]
    [InlineData(PlayscriptStructureLexer.ASYNC, SemanticTokenTypes.Modifier)]
    [InlineData(PlayscriptStructureLexer.STRING_TYPE, SemanticTokenTypes.Type)]
    [InlineData(PlayscriptStructureLexer.INT_TYPE, SemanticTokenTypes.Type)]
    [InlineData(PlayscriptStructureLexer.DECIMAL_TYPE, SemanticTokenTypes.Type)]
    [InlineData(PlayscriptStructureLexer.BOOL_TYPE, SemanticTokenTypes.Type)]
    [InlineData(PlayscriptStructureLexer.VOID_TYPE, SemanticTokenTypes.Type)]
    [InlineData(PlayscriptStructureLexer.IDENTIFIER, SemanticTokenTypes.Variable)]
    [InlineData(PlayscriptStructureLexer.COMMENT, SemanticTokenTypes.Comment)]
    [InlineData(PlayscriptStructureLexer.COLON, SemanticTokenTypes.Operator)]
    [InlineData(PlayscriptStructureLexer.LPAREN, SemanticTokenTypes.Operator)]
    [InlineData(PlayscriptStructureLexer.RPAREN, SemanticTokenTypes.Operator)]
    [InlineData(PlayscriptStructureLexer.COMMA, SemanticTokenTypes.Operator)]
    [InlineData(PlayscriptStructureLexer.LBRACKET, SemanticTokenTypes.Operator)]
    [InlineData(PlayscriptStructureLexer.RBRACKET, SemanticTokenTypes.Operator)]
    public void StructureTokenMap_MapsCorrectly(int tokenType, int expected)
    {
        Assert.Equal(expected, TokenMapping.StructureTokenTypeMap[tokenType]);
    }

    [Theory]
    [InlineData(PlayscriptContentLexer.AT, SemanticTokenTypes.Operator)]
    [InlineData(PlayscriptContentLexer.COMMENT, SemanticTokenTypes.Comment)]
    [InlineData(PlayscriptContentLexer.LPAREN, SemanticTokenTypes.Operator)]
    [InlineData(PlayscriptContentLexer.RPAREN, SemanticTokenTypes.Operator)]
    [InlineData(PlayscriptContentLexer.STRING_LITERAL, SemanticTokenTypes.String)]
    [InlineData(PlayscriptContentLexer.IDENTIFIER, SemanticTokenTypes.Function)]
    [InlineData(PlayscriptContentLexer.COMMA, SemanticTokenTypes.Operator)]
    [InlineData(PlayscriptContentLexer.INTEGER_LITERAL, SemanticTokenTypes.Number)]
    [InlineData(PlayscriptContentLexer.FLOAT_LITERAL, SemanticTokenTypes.Number)]
    [InlineData(PlayscriptContentLexer.BOOLEAN_LITERAL, SemanticTokenTypes.Boolean)]
    [InlineData(PlayscriptContentLexer.SLASH, SemanticTokenTypes.Operator)]
    [InlineData(PlayscriptContentLexer.TEXT, SemanticTokenTypes.String)]
    public void ContentTokenMap_MapsCorrectly(int tokenType, int expected)
    {
        Assert.Equal(expected, TokenMapping.ContentTokenTypeMap[tokenType]);
    }
}
