using System.Collections.Generic;
using MessagePack;

namespace EasyPlayscript;

[MessagePackObject]
public class ConsumerCallItem(string identifier, List<ArgumentValue> arguments) : LineItem
{
    [Key(0)] public string Identifier { get; set; } = identifier;

    [Key(1)] public List<ArgumentValue> Arguments { get; set; } = arguments ?? [];

    [IgnoreMember] public int Line { get; set; }

    [IgnoreMember] public int Col { get; set; }

    [IgnoreMember] public object? Result { get; set; }
}