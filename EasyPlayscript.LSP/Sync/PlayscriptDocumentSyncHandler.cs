using System.Collections.Concurrent;
using EasyPlayscript.LSP.Mapping;
using EasyPlayscript.LSP.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace EasyPlayscript.LSP.Sync;

internal class PlayscriptDocumentSyncHandler(
    DocumentStore store,
    ILanguageServerFacade facade)
    : TextDocumentSyncHandlerBase
{
    private static readonly TextDocumentSelector Selector = new(
        new TextDocumentFilter { Pattern = "**/*.scpt" }
    );

    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);
    private readonly ConcurrentDictionary<DocumentUri, IReadOnlyList<TextDocumentContentChangeEvent>> _pending = new();

    private readonly ConcurrentDictionary<DocumentUri, Timer> _timers = new();

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions
        {
            DocumentSelector = Selector,
            Change = TextDocumentSyncKind.Incremental,
            Save = new SaveOptions { IncludeText = false }
        };
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new TextDocumentAttributes(uri, "playscript");
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
    {
        if (token.IsCancellationRequested) return Unit.Task;

        var uri = notification.TextDocument.Uri;
        CancelDebounce(uri);
        store.OpenOrUpdate(uri, notification.TextDocument.Text);
        PublishDiagnostics(uri, token);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
    {
        if (token.IsCancellationRequested) return Unit.Task;

        var changes = notification.ContentChanges.ToList();
        if (changes.Count == 0) return Unit.Task;

        var uri = notification.TextDocument.Uri;
        _pending.AddOrUpdate(uri, changes, (_, existing) => existing.Concat(changes).ToList());

        if (_timers.TryGetValue(uri, out var existing))
        {
            existing.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
        }
        else
        {
            var timer = new Timer(OnDebounceElapsed, uri, DebounceDelay, Timeout.InfiniteTimeSpan);
            _timers[uri] = timer;
        }

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
    {
        if (token.IsCancellationRequested) return Unit.Task;

        var uri = notification.TextDocument.Uri;
        CancelDebounce(uri);
        store.Close(uri);
        facade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = []
        });
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token)
    {
        return Unit.Task;
    }

    private void OnDebounceElapsed(object? state)
    {
        var uri = (DocumentUri)state!;
        if (!_pending.TryRemove(uri, out var changes)) return;

        _timers.TryRemove(uri, out _);
        store.ApplyChanges(uri, changes);
        PublishDiagnostics(uri, CancellationToken.None);
    }

    private void CancelDebounce(DocumentUri uri)
    {
        if (_timers.TryRemove(uri, out var timer)) timer.Dispose();
        _pending.TryRemove(uri, out _);
    }

    private void PublishDiagnostics(DocumentUri uri, CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        var doc = store.Get(uri);
        if (doc is null) return;

        var diagnostics = doc.Errors
            .Select(e => PositionMapper.ToLspDiagnostic(e, uri))
            .ToArray();

        facade.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = uri,
            Diagnostics = diagnostics
        });
    }
}