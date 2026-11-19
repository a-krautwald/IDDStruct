using System.Text.RegularExpressions;
using IDDStruct.Core.Model;

namespace IDDStruct.Core.Import.Mapping;

/// <summary>
/// Interprets a "type" value (from a CSV column, JSON property or XML element name) as a block kind.
/// Unrecognised types become paragraphs using the type as a custom paragraph style name.
/// </summary>
public static partial class BlockTypeParser
{
    [GeneratedRegex(@"^h([1-6])$", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingTag();

    public static Block Create(string? type, string text, int? level = null, string? style = null)
    {
        var t = (type ?? "").Trim().ToLowerInvariant();
        Block block;
        var h = HeadingTag().Match(t);
        if (h.Success)
            block = Block.Heading(int.Parse(h.Groups[1].Value), text);
        else
        {
            block = t switch
            {
                "title" => Block.Heading(level ?? 1, text),
                "heading" or "header" or "head" or "h" => Block.Heading(level ?? 1, text),
                "subtitle" or "subheading" or "subhead" => Block.Heading(level ?? 2, text),
                "" or "p" or "para" or "paragraph" or "body" or "text" => Block.Paragraph(text),
                "bullet" or "bullets" or "ul" or "li" or "list" or "item" or "list item" => Block.Bullet(text, level ?? 1),
                "numbered" or "number" or "ol" or "numbered list" or "step" => Block.Numbered(text, level ?? 1),
                "pagebreak" or "page break" or "page-break" or "newpage" or "break" => Block.PageBreak(),
                "table" => Block.FromTable(TableData.FromDelimitedText(text)),
                "quote" or "blockquote" => Block.Paragraph(text, "Quote"),
                _ => Block.Paragraph(text, type!.Trim()),
            };
        }
        if (!string.IsNullOrWhiteSpace(style)) block.Style = style.Trim();
        return block;
    }

    public static int? ParseLevel(string? value) =>
        int.TryParse(value?.Trim(), out var n) && n > 0 ? n : null;
}
