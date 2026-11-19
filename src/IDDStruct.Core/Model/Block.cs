namespace IDDStruct.Core.Model;

/// <summary>
/// One structural unit of a document: a heading, paragraph, list item, table or page break.
/// <see cref="Text"/> holds inline markup (<c>**bold**</c>, <c>*italic*</c>); see <see cref="InlineMarkup"/>.
/// </summary>
public sealed class Block
{
    public BlockKind Kind { get; set; } = BlockKind.Paragraph;

    /// <summary>Heading level (1–6) or list nesting level (1–3). Ignored for other kinds.</summary>
    public int Level { get; set; } = 1;

    /// <summary>Optional explicit paragraph style name, overriding the style derived from <see cref="Kind"/>.</summary>
    public string? Style { get; set; }

    public string Text { get; set; } = string.Empty;

    public TableData? Table { get; set; }

    public IReadOnlyList<TextRun> GetRuns() => InlineMarkup.Parse(Text);

    public string PlainText => Kind == BlockKind.Table && Table is not null
        ? string.Join(" ", Table.Rows.SelectMany(r => r).Select(InlineMarkup.ToPlainText))
        : InlineMarkup.ToPlainText(Text);

    public Block Clone() => new()
    {
        Kind = Kind,
        Level = Level,
        Style = Style,
        Text = Text,
        Table = Table?.Clone(),
    };

    public static Block Heading(int level, string text) =>
        new() { Kind = BlockKind.Heading, Level = Math.Clamp(level, 1, 6), Text = text };

    public static Block Paragraph(string text, string? style = null) =>
        new() { Kind = BlockKind.Paragraph, Text = text, Style = style };

    public static Block Bullet(string text, int level = 1) =>
        new() { Kind = BlockKind.BulletItem, Level = Math.Clamp(level, 1, 3), Text = text };

    public static Block Numbered(string text, int level = 1) =>
        new() { Kind = BlockKind.NumberedItem, Level = Math.Clamp(level, 1, 3), Text = text };

    public static Block PageBreak() => new() { Kind = BlockKind.PageBreak };

    public static Block FromTable(TableData table) => new() { Kind = BlockKind.Table, Table = table };

    public override string ToString() => $"{Kind}({Level}): {PlainText}";
}
