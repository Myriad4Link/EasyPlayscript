using EasyPlayscript.LSP.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace EasyPlayscript.LSP.Sync;

internal class PlayscriptSemanticTokensHandler(DocumentStore store)
    : SemanticTokensHandlerBase
{
    private static readonly Container<SemanticTokenType> TokenTypes = new(
        "keyword", "modifier", "type", "function", "string",
        "number", "comment", "operator", "variable",
        new SemanticTokenType("boolean")
    );

    private static readonly Container<SemanticTokenModifier> TokenModifiers = new(
        "declaration", "async"
    );

    protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(
        SemanticTokensCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new SemanticTokensRegistrationOptions
        {
            DocumentSelector = new TextDocumentSelector(
                new TextDocumentFilter { Pattern = "**/*.scpt" }
            ),
            Legend = new SemanticTokensLegend
            {
                TokenTypes = TokenTypes,
                TokenModifiers = TokenModifiers
            },
            Full = new SemanticTokensCapabilityRequestFull { Delta = false },
            Range = true
        };
    }

    protected override Task Tokenize(
        SemanticTokensBuilder builder,
        ITextDocumentIdentifierParams identifier,
        CancellationToken cancellationToken)
    {
        try
        {
            var doc = store.Get(identifier.TextDocument.Uri);
            if (doc is null) return Task.CompletedTask;

            foreach (var token in doc.Tokens)
                builder.Push(token.Line, token.Col, token.Length, token.TokenType, token.TokenModifiers);

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(
        ITextDocumentIdentifierParams @params,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new SemanticTokensDocument(RegistrationOptions.Legend));
    }
}