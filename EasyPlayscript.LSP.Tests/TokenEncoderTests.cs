using EasyPlayscript.LSP.Semantic;

namespace EasyPlayscript.LSP.Tests;

public class TokenEncoderTests
{
    [Fact]
    public void Encode_EmptyList_ReturnsEmptyArray()
    {
        Assert.Empty(TokenEncoder.Encode([]));
    }

    [Fact]
    public void Encode_SingleToken_OnFirstLine()
    {
        var tokens = new List<TokenEntry> { new(0, 0, 6, SemanticTokenTypes.Keyword) };
        var data = TokenEncoder.Encode(tokens);

        Assert.Equal([0, 0, 6, SemanticTokenTypes.Keyword, 0], data);
    }

    [Fact]
    public void Encode_TwoTokens_SameLine_DeltaCol()
    {
        var tokens = new List<TokenEntry>
        {
            new(0, 0, 6, SemanticTokenTypes.Keyword),
            new(0, 7, 4, SemanticTokenTypes.Variable)
        };
        var data = TokenEncoder.Encode(tokens);

        Assert.Equal(10, data.Length);
        Assert.Equal([0, 0, 6, SemanticTokenTypes.Keyword, 0], data[..5]);
        Assert.Equal([0, 7, 4, SemanticTokenTypes.Variable, 0], data[5..]);
    }

    [Fact]
    public void Encode_TwoTokens_DifferentLine()
    {
        var tokens = new List<TokenEntry>
        {
            new(0, 0, 6, SemanticTokenTypes.Keyword),
            new(2, 0, 4, SemanticTokenTypes.Variable)
        };
        var data = TokenEncoder.Encode(tokens);

        Assert.Equal(10, data.Length);
        Assert.Equal(2, data[5]); // deltaLine
        Assert.Equal(0, data[6]); // deltaStart (new line, absolute col)
    }

    [Fact]
    public void Encode_VerifyFiveIntsPerToken()
    {
        var tokens = new List<TokenEntry>
        {
            new(0, 0, 1, 0),
            new(0, 2, 1, 1),
            new(1, 0, 1, 2)
        };
        var data = TokenEncoder.Encode(tokens);

        Assert.Equal(15, data.Length);
    }

    [Fact]
    public void Encode_TokenAtStartOfSecondLine()
    {
        var tokens = new List<TokenEntry>
        {
            new(0, 3, 5, SemanticTokenTypes.String),
            new(1, 0, 4, SemanticTokenTypes.Keyword)
        };
        var data = TokenEncoder.Encode(tokens);

        Assert.Equal(1, data[5]); // deltaLine
        Assert.Equal(0, data[6]); // deltaStart (col 0 on new line)
    }

    [Fact]
    public void Encode_TokenModifiers_Preserved()
    {
        var tokens = new List<TokenEntry>
        {
            new(0, 0, 9, SemanticTokenTypes.Keyword, SemanticTokenModifiers.Declaration)
        };
        var data = TokenEncoder.Encode(tokens);

        Assert.Equal(SemanticTokenModifiers.Declaration, data[4]);
    }
}
