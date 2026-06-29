using System.Reflection;
using EasyPlayscript.LSP.Services;
using EasyPlayscript.LSP.Sync;
using NSubstitute;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace EasyPlayscript.LSP.Tests;

public class PlayscriptDocumentSyncHandlerTests
{
    private static (PlayscriptDocumentSyncHandler handler, DocumentStore store,
        ITextDocumentLanguageServer textDoc) CreateHandler()
    {
        var store = new DocumentStore();
        var facade = Substitute.For<ILanguageServerFacade>();
        var textDoc = Substitute.For<ITextDocumentLanguageServer>();
        facade.TextDocument.Returns(textDoc);
        var handler = new PlayscriptDocumentSyncHandler(store, facade);
        return (handler, store, textDoc);
    }

    private static DidOpenTextDocumentParams OpenParams(DocumentUri uri, string text) =>
        new()
        {
            TextDocument = new TextDocumentItem
            {
                Uri = uri,
                Text = text,
                LanguageId = "playscript",
                Version = 1
            }
        };

    private static DidChangeTextDocumentParams ChangeParams(DocumentUri uri,
        params (Range range, string text)[] changes) =>
        new()
        {
            TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = uri, Version = 2 },
            ContentChanges = changes.Select(c => new TextDocumentContentChangeEvent
            {
                Range = c.range,
                Text = c.text
            }).ToArray()
        };

    private static DidCloseTextDocumentParams CloseParams(DocumentUri uri) =>
        new()
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri }
        };

    private static DidSaveTextDocumentParams SaveParams(DocumentUri uri) =>
        new()
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri }
        };

    // ── DidOpen ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Open_StoresDocument()
    {
        var (handler, store, _) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");

        await handler.Handle(OpenParams(uri, "script a[hello]"), CancellationToken.None);

        Assert.NotNull(store.Get(uri));
        Assert.NotEmpty(store.Get(uri)!.Tokens);
    }

    [Fact]
    public async Task Handle_Open_PublishesDiagnostics()
    {
        var (handler, _, textDoc) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");

        await handler.Handle(OpenParams(uri, "script a[hello]"), CancellationToken.None);

        textDoc.Received(1).PublishDiagnostics(Arg.Is<PublishDiagnosticsParams>(
            p => p.Uri == uri));
    }

    [Fact]
    public async Task Handle_Open_PublishesEmptyDiagnosticsForValidFile()
    {
        var (handler, _, textDoc) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");

        await handler.Handle(OpenParams(uri, "script a[hello]"), CancellationToken.None);

        textDoc.Received(1).PublishDiagnostics(Arg.Is<PublishDiagnosticsParams>(
            p => !p.Diagnostics.Any()));
    }

    [Fact]
    public async Task Handle_Open_PublishesDiagnosticsForErrors()
    {
        var (handler, _, textDoc) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");

        await handler.Handle(OpenParams(uri, "script a"), CancellationToken.None);

        textDoc.Received(1).PublishDiagnostics(Arg.Is<PublishDiagnosticsParams>(
            p => p.Diagnostics.Any()));
    }

    [Fact]
    public async Task Handle_Open_WithCancelledToken_DoesNothing()
    {
        var (handler, store, textDoc) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await handler.Handle(OpenParams(uri, "script a[hello]"), cts.Token);

        Assert.Null(store.Get(uri));
        textDoc.DidNotReceive().PublishDiagnostics(Arg.Any<PublishDiagnosticsParams>());
    }

    // ── DidOpen replaces pending debounce ────────────────────────────────────

    [Fact]
    public async Task Handle_Open_CancelsPendingDebounce()
    {
        var (handler, store, _) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[hello]");
        await handler.Handle(ChangeParams(uri, (new Range(new Position(0, 14), new Position(0, 14)), " world")), CancellationToken.None);

        await handler.Handle(OpenParams(uri, "text b[new content]"), CancellationToken.None);

        Assert.Equal("text b[new content]", store.GetText(uri));
    }

    // ── DidChange (debounce) ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Change_DebouncesThenApplies()
    {
        var (handler, store, textDoc) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[hello]");
        textDoc.ClearReceivedCalls();

        await handler.Handle(
            ChangeParams(uri, (new Range(new Position(0, 14), new Position(0, 14)), " world")),
            CancellationToken.None);

        Assert.Equal("script a[hello]", store.GetText(uri));

        await Task.Delay(400);

        Assert.Equal("script a[hello world]", store.GetText(uri));
        textDoc.Received(1).PublishDiagnostics(Arg.Is<PublishDiagnosticsParams>(
            p => p.Uri == uri));
    }

    [Fact]
    public async Task Handle_Change_AccumulatesMultipleEdits()
    {
        var (handler, store, _) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[ac]");

        await handler.Handle(
            ChangeParams(uri,
                (new Range(new Position(0, 10), new Position(0, 10)), "b"),
                (new Range(new Position(0, 12), new Position(0, 12)), "d")),
            CancellationToken.None);

        await Task.Delay(400);

        Assert.Equal("script a[abcd]", store.GetText(uri));
    }

    [Fact]
    public async Task Handle_Change_DebounceResetsOnNewChange()
    {
        var (handler, store, _) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[hello]");

        await handler.Handle(
            ChangeParams(uri, (new Range(new Position(0, 14), new Position(0, 14)), " world")),
            CancellationToken.None);

        await Task.Delay(200);

        // Position 21 = after "hello world" (14 + len(" world"))
        await handler.Handle(
            ChangeParams(uri, (new Range(new Position(0, 21), new Position(0, 21)), "!")),
            CancellationToken.None);

        Assert.Equal("script a[hello]", store.GetText(uri));

        await Task.Delay(400);

        Assert.Equal("script a[hello world]!", store.GetText(uri));
    }

    [Fact]
    public async Task Handle_Change_IgnoredWhenCancelled()
    {
        var (handler, store, textDoc) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[hello]");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await handler.Handle(
            ChangeParams(uri, (new Range(new Position(0, 14), new Position(0, 14)), " world")),
            cts.Token);

        await Task.Delay(400);

        Assert.Equal("script a[hello]", store.GetText(uri));
        textDoc.DidNotReceive().PublishDiagnostics(Arg.Any<PublishDiagnosticsParams>());
    }

    [Fact]
    public async Task Handle_Change_EmptyChanges_DoesNothing()
    {
        var (handler, store, textDoc) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[hello]");
        textDoc.ClearReceivedCalls();

        var param = new DidChangeTextDocumentParams
        {
            TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = uri, Version = 2 },
            ContentChanges = Array.Empty<TextDocumentContentChangeEvent>()
        };

        await handler.Handle(param, CancellationToken.None);
        await Task.Delay(400);

        textDoc.DidNotReceive().PublishDiagnostics(Arg.Any<PublishDiagnosticsParams>());
    }

    // ── DidClose ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Close_RemovesFromStore()
    {
        var (handler, store, _) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[hello]");
        await handler.Handle(CloseParams(uri), CancellationToken.None);

        Assert.Null(store.Get(uri));
    }

    [Fact]
    public async Task Handle_Close_PublishesEmptyDiagnostics()
    {
        var (handler, _, textDoc) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");

        await handler.Handle(CloseParams(uri), CancellationToken.None);

        textDoc.Received(1).PublishDiagnostics(Arg.Is<PublishDiagnosticsParams>(
            p => p.Uri == uri && !p.Diagnostics.Any()));
    }

    [Fact]
    public async Task Handle_Close_CancelsPendingDebounce()
    {
        var (handler, store, _) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[hello]");

        await handler.Handle(
            ChangeParams(uri, (new Range(new Position(0, 14), new Position(0, 14)), " world")),
            CancellationToken.None);

        await handler.Handle(CloseParams(uri), CancellationToken.None);

        await Task.Delay(400);

        Assert.Null(store.Get(uri));
    }

    [Fact]
    public async Task Handle_Close_WithCancelledToken_DoesNothing()
    {
        var (handler, store, textDoc) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[hello]");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await handler.Handle(CloseParams(uri), cts.Token);

        Assert.NotNull(store.Get(uri));
        textDoc.DidNotReceive().PublishDiagnostics(Arg.Any<PublishDiagnosticsParams>());
    }

    // ── DidSave ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Save_IsNoOp()
    {
        var (handler, store, textDoc) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[hello]");
        textDoc.ClearReceivedCalls();

        await handler.Handle(SaveParams(uri), CancellationToken.None);

        textDoc.DidNotReceive().PublishDiagnostics(Arg.Any<PublishDiagnosticsParams>());
        Assert.Equal("script a[hello]", store.GetText(uri));
    }

    // ── Registration options ─────────────────────────────────────────────────

    [Fact]
    public void GetTextDocumentAttributes_ReturnsPlayscriptLanguage()
    {
        var (handler, _, _) = CreateHandler();
        var uri = DocumentUri.From("/test.scpt");

        var attrs = handler.GetTextDocumentAttributes(uri);

        Assert.Equal("playscript", attrs.LanguageId);
    }

    [Fact]
    public void RegistrationOptions_UsesIncrementalSync()
    {
        var (handler, _, _) = CreateHandler();

        var options = InvokeCreateRegistrationOptions(handler);

        Assert.Equal(TextDocumentSyncKind.Incremental, options.Change);
    }

    [Fact]
    public void RegistrationOptions_SelectsScptFiles()
    {
        var (handler, _, _) = CreateHandler();

        var options = InvokeCreateRegistrationOptions(handler);

        Assert.NotNull(options.DocumentSelector);
        var filters = options.DocumentSelector.ToList();
        Assert.Single(filters);
        Assert.Equal("**/*.scpt", filters[0].Pattern);
    }

    [Fact]
    public void RegistrationOptions_SaveDoesNotIncludeText()
    {
        var (handler, _, _) = CreateHandler();

        var options = InvokeCreateRegistrationOptions(handler);

        Assert.NotNull(options.Save);
        Assert.False(options.Save.Value.IncludeText);
    }

    private static TextDocumentSyncRegistrationOptions InvokeCreateRegistrationOptions(
        PlayscriptDocumentSyncHandler handler)
    {
        var method = typeof(TextDocumentSyncHandlerBase).GetMethod(
            "CreateRegistrationOptions",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (TextDocumentSyncRegistrationOptions)method.Invoke(handler,
            [new TextSynchronizationCapability(), new ClientCapabilities()])!;
    }
}
