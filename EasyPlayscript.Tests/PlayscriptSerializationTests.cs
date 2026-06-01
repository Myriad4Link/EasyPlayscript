using System.Collections.Generic;
using System.Text;
using MessagePack;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptSerializationTests
{
    [Fact]
    public void MessagePack_RoundTrip_EmptyScriptBlock()
    {
        var block = new ScriptBlock();
        var bytes = MessagePackSerializer.Serialize(block);
        var deserialized = MessagePackSerializer.Deserialize<ScriptBlock>(bytes);
        Assert.Empty(deserialized.Pages);
    }

    [Fact]
    public void MessagePack_RoundTrip_WithContent()
    {
        var block = new ScriptBlock
        {
            Pages = new List<Page>
            {
                new Page
                {
                    Paragraphs = new List<Paragraph>
                    {
                        new Paragraph
                        {
                            Lines = new List<Line>
                            {
                                new Line
                                {
                                    Items = new List<LineItem>
                                    {
                                        new TextItem("Hello"),
                                        new ConsumerCallItem("transition", new List<ArgumentValue> { new StringArgument("fade") })
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
        var bytes = MessagePackSerializer.Serialize(block);
        var deserialized = MessagePackSerializer.Deserialize<ScriptBlock>(bytes);
        Assert.Single(deserialized.Pages);
        Assert.Equal("Hello", ((TextItem)deserialized.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
        Assert.Equal("transition", ((ConsumerCallItem)deserialized.Pages[0].Paragraphs[0].Lines[0].Items[1]).Identifier);
        Assert.Single(((ConsumerCallItem)deserialized.Pages[0].Paragraphs[0].Lines[0].Items[1]).Arguments);
        Assert.IsType<StringArgument>(((ConsumerCallItem)deserialized.Pages[0].Paragraphs[0].Lines[0].Items[1]).Arguments[0]);
        Assert.Equal("fade", ((StringArgument)((ConsumerCallItem)deserialized.Pages[0].Paragraphs[0].Lines[0].Items[1]).Arguments[0]).Value);
    }

    [Fact]
    public void MessagePack_RoundTrip_MultiplePages()
    {
        var block = new ScriptBlock
        {
            Pages = new List<Page>
            {
                new Page
                {
                    Paragraphs = new List<Paragraph>
                    {
                        new Paragraph
                        {
                            Lines = new List<Line>
                            {
                                new Line { Items = new List<LineItem> { new TextItem("page 1") } }
                            }
                        }
                    }
                },
                new Page
                {
                    Paragraphs = new List<Paragraph>
                    {
                        new Paragraph
                        {
                            Lines = new List<Line>
                            {
                                new Line { Items = new List<LineItem> { new TextItem("page 2") } }
                            }
                        }
                    }
                }
            }
        };
        var bytes = MessagePackSerializer.Serialize(block);
        var deserialized = MessagePackSerializer.Deserialize<ScriptBlock>(bytes);
        Assert.Equal(2, deserialized.Pages.Count);
        Assert.Equal("page 1", ((TextItem)deserialized.Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
        Assert.Equal("page 2", ((TextItem)deserialized.Pages[1].Paragraphs[0].Lines[0].Items[0]).Text);
    }

    [Fact]
    public void MessagePack_RoundTrip_PlayscriptData()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptBlock>
            {
                ["test"] = new ScriptBlock
                {
                    Pages = new List<Page>
                    {
                        new Page
                        {
                            Paragraphs = new List<Paragraph>
                            {
                                new Paragraph
                                {
                                    Lines = new List<Line>
                                    {
                                        new Line { Items = new List<LineItem> { new TextItem("Hello") } }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            Texts = new Dictionary<string, ScriptBlock>
            {
                ["intro"] = new ScriptBlock
                {
                    Pages = new List<Page>
                    {
                        new Page
                        {
                            Paragraphs = new List<Paragraph>
                            {
                                new Paragraph
                                {
                                    Lines = new List<Line>
                                    {
                                        new Line { Items = new List<LineItem> { new TextItem("Welcome") } }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
        var bytes = MessagePackSerializer.Serialize(data);
        var deserialized = MessagePackSerializer.Deserialize<PlayscriptData>(bytes);
        Assert.Single(deserialized.Scripts);
        Assert.Single(deserialized.Texts);
        Assert.Equal("Hello", ((TextItem)deserialized.Scripts["test"].Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
        Assert.Equal("Welcome", ((TextItem)deserialized.Texts["intro"].Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
    }

    [Fact]
    public void Aes_RoundTrip()
    {
        var original = Encoding.UTF8.GetBytes("Hello world");
        var key = "test-key-1234567";
        var encrypted = PlayscriptLoader.AesEncrypt(original, key);
        var decrypted = PlayscriptLoader.AesDecrypt(encrypted, key);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Aes_DifferentKeys_ProduceDifferentCiphertext()
    {
        var original = Encoding.UTF8.GetBytes("Hello world");
        var encrypted1 = PlayscriptLoader.AesEncrypt(original, "key-one");
        var encrypted2 = PlayscriptLoader.AesEncrypt(original, "key-two");
        Assert.NotEqual(encrypted1, encrypted2);
    }

    [Fact]
    public void Aes_EncryptedDataStartsWithIV()
    {
        var original = Encoding.UTF8.GetBytes("Hello world");
        var encrypted = PlayscriptLoader.AesEncrypt(original, "test-key");
        // AES block size is 16 bytes, IV is prepended
        Assert.True(encrypted.Length > 16);
        // The encrypted data should be different from original
        Assert.NotEqual(original, encrypted);
    }

    [Fact]
    public void FullPipeline_SerializeEncryptDeserialize()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptBlock>
            {
                ["greeting"] = new ScriptBlock
                {
                    Pages = new List<Page>
                    {
                        new Page
                        {
                            Paragraphs = new List<Paragraph>
                            {
                                new Paragraph
                                {
                                    Lines = new List<Line>
                                    {
                                        new Line
                                        {
                                            Items = new List<LineItem>
                                            {
                                                new TextItem("Hello "),
                                                new ConsumerCallItem("transition", new List<ArgumentValue> { new StringArgument("fade_out") }),
                                                new TextItem(" world")
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            Texts = new Dictionary<string, ScriptBlock>()
        };

        var key = "pipeline-test-key";
        var bytes = MessagePackSerializer.Serialize(data);
        var encrypted = PlayscriptLoader.AesEncrypt(bytes, key);
        var decrypted = PlayscriptLoader.AesDecrypt(encrypted, key);
        var deserialized = MessagePackSerializer.Deserialize<PlayscriptData>(decrypted);

        Assert.Single(deserialized.Scripts);
        Assert.Empty(deserialized.Texts);

        var items = deserialized.Scripts["greeting"].Pages[0].Paragraphs[0].Lines[0].Items;
        Assert.Equal(3, items.Count);
        Assert.Equal("Hello ", ((TextItem)items[0]).Text);
        Assert.Equal("transition", ((ConsumerCallItem)items[1]).Identifier);
        Assert.Single(((ConsumerCallItem)items[1]).Arguments);
        Assert.IsType<StringArgument>(((ConsumerCallItem)items[1]).Arguments[0]);
        Assert.Equal("fade_out", ((StringArgument)((ConsumerCallItem)items[1]).Arguments[0]).Value);
        Assert.Equal(" world", ((TextItem)items[2]).Text);
    }
}
