using EasyPlayscript.LSP.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace EasyPlayscript.LSP.Sync;

internal class PlayscriptFormattingHandler(
    DocumentStore store,
    PlayscriptFormatter formatter)
    : DocumentFormattingHandlerBase
{
    private static readonly TextDocumentSelector Selector = new(
        new TextDocumentFilter { Pattern = "**/*.scpt" }
    );

    protected override DocumentFormattingRegistrationOptions CreateRegistrationOptions(
        DocumentFormattingCapability capability, ClientCapabilities clientCapabilities)
    {
        return new DocumentFormattingRegistrationOptions
        {
            DocumentSelector = Selector
        };
    }

    public override Task<TextEditContainer?> Handle(
        DocumentFormattingParams request,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult<TextEditContainer?>(null);

        var uri = request.TextDocument.Uri;
        var text = store.GetText(uri);
        if (text is null)
            return Task.FromResult<TextEditContainer?>(null);

        var formatted = formatter.Format(text);
        if (formatted == text)
            return Task.FromResult<TextEditContainer?>(null);

        var edit = new TextEdit
        {
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range
            {
                Start = new Position(0, 0),
                End = new Position(int.MaxValue, int.MaxValue)
            },
            NewText = formatted
        };

        return Task.FromResult<TextEditContainer?>(new TextEditContainer(edit));
    }
}
