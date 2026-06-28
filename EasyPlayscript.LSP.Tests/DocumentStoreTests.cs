using EasyPlayscript.LSP.Services;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace EasyPlayscript.LSP.Tests;

public class DocumentStoreTests
{
    private static DocumentStore CreateStore() =>
        new();

    [Fact]
    public void OpenNewDocument_StoresAndReturns()
    {
        var store = CreateStore();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[hello]");
        var doc = store.Get(uri);

        Assert.NotNull(doc);
        Assert.NotEmpty(doc.Tokens);
    }

    [Fact]
    public void UpdateDocument_ReplacesContent()
    {
        var store = CreateStore();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[hello]");
        var before = store.Get(uri)!.Tokens.Count;

        store.OpenOrUpdate(uri, "script a[hello world] text b[extra]");
        var after = store.Get(uri)!.Tokens.Count;

        Assert.True(after > before);
    }

    [Fact]
    public void CloseDocument_Removes()
    {
        var store = CreateStore();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[hello]");
        store.Close(uri);

        Assert.Null(store.Get(uri));
    }

    [Fact]
    public void GetNonExistent_ReturnsNull()
    {
        var store = CreateStore();
        Assert.Null(store.Get(DocumentUri.From("/nope.scpt")));
    }
}
