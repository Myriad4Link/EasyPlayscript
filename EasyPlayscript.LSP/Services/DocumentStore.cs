using System.Collections.Concurrent;
using EasyPlayscript.LSP.Parsing;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace EasyPlayscript.LSP.Services;

internal class DocumentStore
{
    private readonly ConcurrentDictionary<DocumentUri, ParsedDocument> _docs = new();

    public void OpenOrUpdate(DocumentUri uri, string text)
    {
        var doc = PlayscriptDocumentParser.Parse(text);
        _docs[uri] = doc;
    }

    public void Close(DocumentUri uri)
    {
        _docs.TryRemove(uri, out _);
    }

    public ParsedDocument? Get(DocumentUri uri)
    {
        return _docs.GetValueOrDefault(uri);
    }
}