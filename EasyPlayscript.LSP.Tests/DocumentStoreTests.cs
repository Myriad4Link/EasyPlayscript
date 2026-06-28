using EasyPlayscript.LSP.Semantic;
using EasyPlayscript.LSP.Services;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

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

    // ── GetText ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetText_ReturnsCurrentText()
    {
        var store = CreateStore();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[hello]");

        Assert.Equal("script a[hello]", store.GetText(uri));
    }

    [Fact]
    public void GetText_AfterApplyChanges_ReturnsUpdatedText()
    {
        var store = CreateStore();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[hello]");
        // "script a[hello]" → positions: s=0 c=1 r=2 i=3 p=4 t=5 ' '=6 a=7 [=8 h=9 e=10 l=11 l=12 o=13 ]=14
        // Insert " world" at position 14 (before ']') → "script a[hello world]"
        store.ApplyChanges(uri, [
            new TextDocumentContentChangeEvent
            {
                Range = new Range(new Position(0, 14), new Position(0, 14)),
                Text = " world"
            }
        ]);

        Assert.Equal("script a[hello world]", store.GetText(uri));
    }

    [Fact]
    public void GetText_NonExistent_ReturnsNull()
    {
        var store = CreateStore();
        Assert.Null(store.GetText(DocumentUri.From("/nope.scpt")));
    }

    [Fact]
    public void Close_RemovesText()
    {
        var store = CreateStore();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[hello]");
        store.Close(uri);

        Assert.Null(store.GetText(uri));
    }

    // ── ApplyChanges ────────────────────────────────────────────────────────

    [Fact]
    public void ApplyChanges_InsertText_UpdatesDocument()
    {
        var store = CreateStore();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[helloworld]");
        // "script a[helloworld]" → h=9 e=10 l=11 l=12 o=13 w=14
        // Insert " " at position 14 (between "hello" and "world")
        store.ApplyChanges(uri, [
            new TextDocumentContentChangeEvent
            {
                Range = new Range(new Position(0, 14), new Position(0, 14)),
                Text = " "
            }
        ]);

        var after = store.Get(uri)!;
        Assert.Equal("script a[hello world]", after.Text);
        Assert.NotEmpty(after.Tokens);
    }

    [Fact]
    public void ApplyChanges_MultipleEdits_UpdatesDocument()
    {
        var store = CreateStore();
        var uri = DocumentUri.From("/test.scpt");

        // "script a[ac]" → [=8 a=9 c=10 ]=11
        store.OpenOrUpdate(uri, "script a[ac]");
        store.ApplyChanges(uri, [
            new TextDocumentContentChangeEvent
            {
                Range = new Range(new Position(0, 10), new Position(0, 10)),
                Text = "b"
            },
            new TextDocumentContentChangeEvent
            {
                Range = new Range(new Position(0, 12), new Position(0, 12)),
                Text = "d"
            }
        ]);

        Assert.Equal("script a[abcd]", store.GetText(uri));
    }

    [Fact]
    public void ApplyChanges_UsesIncrementalParse()
    {
        var store = CreateStore();
        var uri = DocumentUri.From("/test.scpt");

        store.OpenOrUpdate(uri, "script a[hello]\ntext b[world]");

        // Edit block "a" — block "b" tokens should be unchanged in position
        var beforeB = store.Get(uri)!.Tokens
            .Where(t => t.TokenType == SemanticTokenTypes.String && t.Line >= 1)
            .OrderBy(t => t.Col).ToArray();

        store.ApplyChanges(uri, [
            new TextDocumentContentChangeEvent
            {
                Range = new Range(new Position(0, 9), new Position(0, 14)),
                Text = "hi"
            }
        ]);

        var afterB = store.Get(uri)!.Tokens
            .Where(t => t.TokenType == SemanticTokenTypes.String && t.Line >= 1)
            .OrderBy(t => t.Col).ToArray();

        Assert.Equal(beforeB.Length, afterB.Length);
        for (var i = 0; i < beforeB.Length; i++)
        {
            Assert.Equal(beforeB[i].Line, afterB[i].Line);
            Assert.Equal(beforeB[i].Col, afterB[i].Col);
        }
    }

    [Fact]
    public void ApplyChanges_NoPreviousOpen_ParsesFromScratch()
    {
        var store = CreateStore();
        var uri = DocumentUri.From("/test.scpt");

        store.ApplyChanges(uri, [
            new TextDocumentContentChangeEvent
            {
                Text = "script a[hello]"
            }
        ]);

        var doc = store.Get(uri);
        Assert.NotNull(doc);
        Assert.Equal("script a[hello]", doc.Text);
        Assert.NotEmpty(doc.Tokens);
    }
}
