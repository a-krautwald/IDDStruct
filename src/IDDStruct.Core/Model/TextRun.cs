namespace IDDStruct.Core.Model;

/// <summary>A span of text sharing the same inline formatting.</summary>
public sealed record TextRun(string Text, bool Bold = false, bool Italic = false);
