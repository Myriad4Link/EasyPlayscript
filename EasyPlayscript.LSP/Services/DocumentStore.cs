using System.Collections.Concurrent;
using EasyPlayscript.LSP.Parsing;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace EasyPlayscript.LSP.Services;

internal class DocumentStore
{
    private readonly ConcurrentDictionary<DocumentUri, ParsedDocument> _docs = new();
    private readonly ConcurrentDictionary<DocumentUri, string> _texts = new();

    public void OpenOrUpdate(DocumentUri uri, string text)
    {
        _texts[uri] = text;
        var doc = PlayscriptDocumentParser.ParseIncremental(text, Get(uri));
        _docs[uri] = doc;
    }

    public ParsedDocument ApplyChanges(DocumentUri uri, IReadOnlyList<TextDocumentContentChangeEvent> changes)
    {
        var currentText = _texts.GetValueOrDefault(uri) ?? "";
        var newText = TextEditApplier.ApplyChanges(currentText, changes);
        _texts[uri] = newText;

        var previous = Get(uri);
        var doc = PlayscriptDocumentParser.ParseIncremental(newText, previous);
        _docs[uri] = doc;
        return doc;
    }

    public void Close(DocumentUri uri)
    {
        _docs.TryRemove(uri, out _);
        _texts.TryRemove(uri, out _);
    }

    public ParsedDocument? Get(DocumentUri uri)
    {
        return _docs.GetValueOrDefault(uri);
    }

    public string? GetText(DocumentUri uri)
    {
        return _texts.GetValueOrDefault(uri);
    }
}