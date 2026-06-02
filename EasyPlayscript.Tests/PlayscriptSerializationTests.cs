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
            Texts = new Dictionary<string, TextBlock>
            {
                ["intro"] = new TextBlock
                {
                    Items = new List<LineItem> { new TextItem("Welcome") }
                }
            }
        };
        var bytes = MessagePackSerializer.Serialize(data);
        var deserialized = MessagePackSerializer.Deserialize<PlayscriptData>(bytes);
        Assert.Single(deserialized.Scripts);
        Assert.Single(deserialized.Texts);
        Assert.Equal("Hello", ((TextItem)deserialized.Scripts["test"].Pages[0].Paragraphs[0].Lines[0].Items[0]).Text);
        Assert.Equal("Welcome", ((TextItem)deserialized.Texts["intro"].Items[0]).Text);
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
            Texts = new Dictionary<string, TextBlock>()
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

    [Fact]
    public void TextBlock_RoundTrip_Empty()
    {
        var block = new TextBlock();
        var bytes = MessagePackSerializer.Serialize(block);
        var deserialized = MessagePackSerializer.Deserialize<TextBlock>(bytes);
        Assert.Empty(deserialized.Items);
    }

    [Fact]
    public void TextBlock_RoundTrip_SingleTextItem()
    {
        var block = new TextBlock { Items = new List<LineItem> { new TextItem("Hello") } };
        var bytes = MessagePackSerializer.Serialize(block);
        var deserialized = MessagePackSerializer.Deserialize<TextBlock>(bytes);
        Assert.Single(deserialized.Items);
        Assert.Equal("Hello", ((TextItem)deserialized.Items[0]).Text);
    }

    [Fact]
    public void TextBlock_RoundTrip_MultipleItems()
    {
        var block = new TextBlock
        {
            Items = new List<LineItem>
            {
                new TextItem("Hello "),
                new TextItem("World")
            }
        };
        var bytes = MessagePackSerializer.Serialize(block);
        var deserialized = MessagePackSerializer.Deserialize<TextBlock>(bytes);
        Assert.Equal(2, deserialized.Items.Count);
        Assert.Equal("Hello ", ((TextItem)deserialized.Items[0]).Text);
        Assert.Equal("World", ((TextItem)deserialized.Items[1]).Text);
    }

    [Fact]
    public void TextBlock_RoundTrip_MixedTextAndConsumerCall()
    {
        var block = new TextBlock
        {
            Items = new List<LineItem>
            {
                new TextItem("Hi, "),
                new ConsumerCallItem("get_name", new List<ArgumentValue> { new StringArgument("test") }),
                new TextItem(".")
            }
        };
        var bytes = MessagePackSerializer.Serialize(block);
        var deserialized = MessagePackSerializer.Deserialize<TextBlock>(bytes);
        Assert.Equal(3, deserialized.Items.Count);
        Assert.Equal("Hi, ", ((TextItem)deserialized.Items[0]).Text);
        Assert.Equal("get_name", ((ConsumerCallItem)deserialized.Items[1]).Identifier);
        Assert.Single(((ConsumerCallItem)deserialized.Items[1]).Arguments);
        Assert.IsType<StringArgument>(((ConsumerCallItem)deserialized.Items[1]).Arguments[0]);
        Assert.Equal("test", ((StringArgument)((ConsumerCallItem)deserialized.Items[1]).Arguments[0]).Value);
        Assert.Equal(".", ((TextItem)deserialized.Items[2]).Text);
    }

    [Fact]
    public void TextBlock_RoundTrip_AllArgumentTypes()
    {
        var block = new TextBlock
        {
            Items = new List<LineItem>
            {
                new ConsumerCallItem("do_thing", new List<ArgumentValue>
                {
                    new StringArgument("hello"),
                    new IntArgument(42),
                    new DoubleArgument(3.14),
                    new BoolArgument(true)
                })
            }
        };
        var bytes = MessagePackSerializer.Serialize(block);
        var deserialized = MessagePackSerializer.Deserialize<TextBlock>(bytes);
        Assert.Single(deserialized.Items);
        var call = (ConsumerCallItem)deserialized.Items[0];
        Assert.Equal("do_thing", call.Identifier);
        Assert.Equal(4, call.Arguments.Count);
        Assert.IsType<StringArgument>(call.Arguments[0]);
        Assert.Equal("hello", ((StringArgument)call.Arguments[0]).Value);
        Assert.IsType<IntArgument>(call.Arguments[1]);
        Assert.Equal(42, ((IntArgument)call.Arguments[1]).Value);
        Assert.IsType<DoubleArgument>(call.Arguments[2]);
        Assert.Equal(3.14, ((DoubleArgument)call.Arguments[2]).Value);
        Assert.IsType<BoolArgument>(call.Arguments[3]);
        Assert.True(((BoolArgument)call.Arguments[3]).Value);
    }

    [Fact]
    public void PlayscriptLoader_LoadTexts_ReturnsTextBlock()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptBlock>(),
            Texts = new Dictionary<string, TextBlock>
            {
                ["intro"] = new TextBlock
                {
                    Items = new List<LineItem>
                    {
                        new TextItem("Welcome, "),
                        new ConsumerCallItem("get_name", new List<ArgumentValue>()),
                        new TextItem("!")
                    }
                }
            }
        };

        var key = "loader-test-key";
        var bytes = MessagePackSerializer.Serialize(data);
        var encrypted = PlayscriptLoader.AesEncrypt(bytes, key);
        var tempPath = System.IO.Path.GetTempFileName();
        try
        {
            System.IO.File.WriteAllBytes(tempPath, encrypted);
            var result = PlayscriptLoader.LoadTexts(tempPath, key);

            Assert.Single(result);
            Assert.IsType<TextBlock>(result["intro"]);
            var items = result["intro"].Items;
            Assert.Equal(3, items.Count);
            Assert.Equal("Welcome, ", ((TextItem)items[0]).Text);
            Assert.Equal("get_name", ((ConsumerCallItem)items[1]).Identifier);
            Assert.Equal("!", ((TextItem)items[2]).Text);
        }
        finally
        {
            System.IO.File.Delete(tempPath);
        }
    }

    [Fact]
    public void PlayscriptData_RoundTrip_TextsUseTextBlock()
    {
        var data = new PlayscriptData
        {
            Scripts = new Dictionary<string, ScriptBlock>(),
            Texts = new Dictionary<string, TextBlock>
            {
                ["intro"] = new TextBlock
                {
                    Items = new List<LineItem>
                    {
                        new TextItem("Welcome, "),
                        new ConsumerCallItem("get_name", new List<ArgumentValue>()),
                        new TextItem("!")
                    }
                }
            }
        };
        var bytes = MessagePackSerializer.Serialize(data);
        var deserialized = MessagePackSerializer.Deserialize<PlayscriptData>(bytes);
        Assert.Empty(deserialized.Scripts);
        Assert.Single(deserialized.Texts);
        Assert.IsType<TextBlock>(deserialized.Texts["intro"]);
        var items = deserialized.Texts["intro"].Items;
        Assert.Equal(3, items.Count);
        Assert.Equal("Welcome, ", ((TextItem)items[0]).Text);
        Assert.Equal("get_name", ((ConsumerCallItem)items[1]).Identifier);
        Assert.Equal("!", ((TextItem)items[2]).Text);
    }

    // ─── Phase 10: Script Block Verification ────────────────────────────────

    [Fact]
    public void ScriptBlock_RoundTrip_UnchangedAfterTextBlockRefactor()
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
                                        new TextItem("Hello "),
                                        new ConsumerCallItem("get_name", new List<ArgumentValue>()),
                                        new TextItem(".")
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
        var items = deserialized.Pages[0].Paragraphs[0].Lines[0].Items;
        Assert.Equal(3, items.Count);
        Assert.Equal("Hello ", ((TextItem)items[0]).Text);
        Assert.Equal("get_name", ((ConsumerCallItem)items[1]).Identifier);
        Assert.Empty(((ConsumerCallItem)items[1]).Arguments);
        Assert.Equal(".", ((TextItem)items[2]).Text);
    }

    [Fact]
    public void PlayscriptData_RoundTrip_ScriptAndTextBlockCoexist()
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
                                                new TextItem("Hello"),
                                                new ConsumerCallItem("transition", new List<ArgumentValue> { new StringArgument("fade") })
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            Texts = new Dictionary<string, TextBlock>
            {
                ["intro"] = new TextBlock
                {
                    Items = new List<LineItem>
                    {
                        new TextItem("Welcome, "),
                        new ConsumerCallItem("get_name", new List<ArgumentValue>()),
                        new TextItem("!")
                    }
                }
            }
        };
        var bytes = MessagePackSerializer.Serialize(data);
        var deserialized = MessagePackSerializer.Deserialize<PlayscriptData>(bytes);

        Assert.Single(deserialized.Scripts);
        Assert.IsType<ScriptBlock>(deserialized.Scripts["greeting"]);
        var scriptItems = deserialized.Scripts["greeting"].Pages[0].Paragraphs[0].Lines[0].Items;
        Assert.Equal("Hello", ((TextItem)scriptItems[0]).Text);
        Assert.Equal("transition", ((ConsumerCallItem)scriptItems[1]).Identifier);

        Assert.Single(deserialized.Texts);
        Assert.IsType<TextBlock>(deserialized.Texts["intro"]);
        var textItems = deserialized.Texts["intro"].Items;
        Assert.Equal("Welcome, ", ((TextItem)textItems[0]).Text);
        Assert.Equal("get_name", ((ConsumerCallItem)textItems[1]).Identifier);
        Assert.Equal("!", ((TextItem)textItems[2]).Text);
    }
}
