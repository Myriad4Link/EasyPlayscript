using System.Collections.Generic;

namespace EasyPlayscript.Parsing;

public class StructureParseResult(List<StructureResult> results, List<InterfaceDeclaration> interfaces)
{
    public List<StructureResult> Results { get; } = results;
    public List<InterfaceDeclaration> Interfaces { get; } = interfaces;
    public string? Namespace { get; set; }
}

/// <summary>
///     Represents a parsed structure result from Pass 1, containing compiler call info and optional raw block content.
/// </summary>
public readonly struct StructureResult(BlockType identifier, string name, string? variation, bool isDefault, string? rawContent, int line, int col, int startChar, int endChar)
{
    public BlockType Identifier { get; } = identifier;
    public string Name { get; } = name;
    public string? Variation { get; } = variation;
    public bool IsDefault { get; } = isDefault;
    public string? RawContent { get; } = rawContent;
    public int Line { get; } = line;
    public int Col { get; } = col;
    public int StartChar { get; } = startChar;
    public int EndChar { get; } = endChar;
}