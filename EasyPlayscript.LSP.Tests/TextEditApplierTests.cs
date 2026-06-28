using EasyPlayscript.LSP.Parsing;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace EasyPlayscript.LSP.Tests;

public class TextEditApplierTests
{
    // ── PositionToOffset ────────────────────────────────────────────────────

    [Fact]
    public void PositionToOffset_FirstLine()
    {
        var offset = TextEditApplier.PositionToOffset("hello", new Position(0, 3));
        Assert.Equal(3, offset);
    }

    [Fact]
    public void PositionToOffset_SecondLine()
    {
        var offset = TextEditApplier.PositionToOffset("hello\nworld", new Position(1, 0));
        Assert.Equal(6, offset);
    }

    [Fact]
    public void PositionToOffset_EndOfLine()
    {
        var offset = TextEditApplier.PositionToOffset("hello", new Position(0, 5));
        Assert.Equal(5, offset);
    }

    [Fact]
    public void PositionToOffset_BeyondContent_Clamps()
    {
        var offset = TextEditApplier.PositionToOffset("hi", new Position(0, 99));
        Assert.Equal(2, offset);
    }

    [Fact]
    public void PositionToOffset_BeyondLastLine_Clamps()
    {
        var offset = TextEditApplier.PositionToOffset("hi", new Position(99, 0));
        Assert.Equal(2, offset);
    }

    [Fact]
    public void PositionToOffset_EmptyText()
    {
        var offset = TextEditApplier.PositionToOffset("", new Position(0, 0));
        Assert.Equal(0, offset);
    }

    [Fact]
    public void PositionToOffset_MultipleLines()
    {
        var offset = TextEditApplier.PositionToOffset("aa\nbb\ncc", new Position(2, 1));
        Assert.Equal(7, offset); // "aa\nbb\n" = 6, 'c' at col 0 = 6, 'c' at col 1 = 7
    }

    // ── ApplyChanges ────────────────────────────────────────────────────────

    [Fact]
    public void ApplyChanges_EmptyText_InsertText()
    {
        var result = TextEditApplier.ApplyChanges("", [
            new TextDocumentContentChangeEvent
            {
                Range = new Range(new Position(0, 0), new Position(0, 0)),
                Text = "hello"
            }
        ]);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ApplyChanges_SingleCharInsert()
    {
        var result = TextEditApplier.ApplyChanges("bc", [
            new TextDocumentContentChangeEvent
            {
                Range = new Range(new Position(0, 0), new Position(0, 0)),
                Text = "a"
            }
        ]);
        Assert.Equal("abc", result);
    }

    [Fact]
    public void ApplyChanges_MultiLineInsert()
    {
        var result = TextEditApplier.ApplyChanges("hello", [
            new TextDocumentContentChangeEvent
            {
                Range = new Range(new Position(0, 5), new Position(0, 5)),
                Text = "\nworld"
            }
        ]);
        Assert.Equal("hello\nworld", result);
    }

    [Fact]
    public void ApplyChanges_DeleteRange()
    {
        var result = TextEditApplier.ApplyChanges("abcd", [
            new TextDocumentContentChangeEvent
            {
                Range = new Range(new Position(0, 1), new Position(0, 3)),
                Text = ""
            }
        ]);
        Assert.Equal("ad", result);
    }

    [Fact]
    public void ApplyChanges_ReplaceRange()
    {
        var result = TextEditApplier.ApplyChanges("hello world", [
            new TextDocumentContentChangeEvent
            {
                Range = new Range(new Position(0, 0), new Position(0, 5)),
                Text = "goodbye"
            }
        ]);
        Assert.Equal("goodbye world", result);
    }

    [Fact]
    public void ApplyChanges_MultipleEdits()
    {
        var result = TextEditApplier.ApplyChanges("ac", [
            new TextDocumentContentChangeEvent
            {
                Range = new Range(new Position(0, 1), new Position(0, 1)),
                Text = "b"
            },
            new TextDocumentContentChangeEvent
            {
                Range = new Range(new Position(0, 3), new Position(0, 3)),
                Text = "d"
            }
        ]);
        Assert.Equal("abcd", result);
    }

    [Fact]
    public void ApplyChanges_NullRange_ReplacesAll()
    {
        var result = TextEditApplier.ApplyChanges("old content", [
            new TextDocumentContentChangeEvent
            {
                Text = "new content"
            }
        ]);
        Assert.Equal("new content", result);
    }

    [Fact]
    public void ApplyChanges_MultiLine_DeleteAcrossLines()
    {
        var result = TextEditApplier.ApplyChanges("line1\nline2\nline3", [
            new TextDocumentContentChangeEvent
            {
                Range = new Range(new Position(0, 3), new Position(2, 3)),
                Text = ""
            }
        ]);
        Assert.Equal("line3", result); // deletes "e1\nline2\nlin" (offset 3..15)
    }

    [Fact]
    public void ApplyChanges_InsertNewline()
    {
        var result = TextEditApplier.ApplyChanges("ab", [
            new TextDocumentContentChangeEvent
            {
                Range = new Range(new Position(0, 1), new Position(0, 1)),
                Text = "\n"
            }
        ]);
        Assert.Equal("a\nb", result);
    }

    [Fact]
    public void ApplyChanges_DeleteNewline()
    {
        var result = TextEditApplier.ApplyChanges("a\nb", [
            new TextDocumentContentChangeEvent
            {
                Range = new Range(new Position(0, 1), new Position(1, 0)),
                Text = ""
            }
        ]);
        Assert.Equal("ab", result);
    }

    [Fact]
    public void ApplyChanges_ReplaceAcrossLines()
    {
        var result = TextEditApplier.ApplyChanges("aaa\nbbb\nccc", [
            new TextDocumentContentChangeEvent
            {
                Range = new Range(new Position(0, 2), new Position(2, 1)),
                Text = "X"
            }
        ]);
        Assert.Equal("aaXcc", result);
    }

    [Fact]
    public void ApplyChanges_NoChanges_ReturnsOriginal()
    {
        var result = TextEditApplier.ApplyChanges("hello", []);
        Assert.Equal("hello", result);
    }
}
