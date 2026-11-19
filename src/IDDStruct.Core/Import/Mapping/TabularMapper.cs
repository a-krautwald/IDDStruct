using System.Text.RegularExpressions;
using IDDStruct.Core.Model;

namespace IDDStruct.Core.Import.Mapping;

/// <summary>
/// Maps rows and columns to long-form structure. Two modes:
/// <list type="bullet">
/// <item><b>Block mode</b> – columns such as <c>type</c>, <c>text</c>, <c>level</c>, <c>style</c>:
/// each row is one block (heading, paragraph, bullet…).</item>
/// <item><b>Record mode</b> – any other table: each row becomes a section with a heading
/// (from the heading column) followed by one paragraph per remaining field.</item>
/// </list>
/// </summary>
public static partial class TabularMapper
{
    private static readonly string[] TypeColumns = { "type", "kind", "block", "element" };
    private static readonly string[] TextColumns = { "text", "content", "value", "body" };
    private static readonly string[] LevelColumns = { "level", "depth" };
    private static readonly string[] StyleColumns = { "style", "paragraph style", "paragraphstyle" };

    [GeneratedRegex(@"\r?\n[ \t]*\r?\n")]
    private static partial Regex BlankLine();

    public static bool LooksLikeBlockList(TabularData data) => LooksLikeBlockList(data.Headers);

    /// <summary>True when the headers describe one block per row (a text column plus a type or style column).</summary>
    public static bool LooksLikeBlockList(IEnumerable<string> headers)
    {
        var set = new HashSet<string>(headers.Select(h => h.Trim()), StringComparer.OrdinalIgnoreCase);
        return TextColumns.Any(set.Contains) && (TypeColumns.Any(set.Contains) || StyleColumns.Any(set.Contains));
    }

    public static List<Block> Map(TabularData data, ImportOptions options, List<string> warnings)
    {
        if (data.Headers.Count == 0) return new List<Block>();
        return !options.ForceRecordMode && LooksLikeBlockList(data)
            ? MapBlocks(data)
            : MapRecords(data, options, warnings);
    }

    private static int Find(TabularData data, string[] names) =>
        names.Select(data.IndexOf).FirstOrDefault(i => i >= 0, -1);

    private static List<Block> MapBlocks(TabularData data)
    {
        int type = Find(data, TypeColumns), text = Find(data, TextColumns);
        int level = Find(data, LevelColumns), style = Find(data, StyleColumns);
        var blocks = new List<Block>();
        foreach (var row in data.Rows)
        {
            var typeValue = type >= 0 ? data.Cell(row, type) : "";
            var textValue = data.Cell(row, text).Replace("\r\n", "\n");
            var styleValue = style >= 0 ? data.Cell(row, style) : null;
            if (typeValue.Trim().Length == 0 && textValue.Trim().Length == 0 && string.IsNullOrWhiteSpace(styleValue))
                continue;
            blocks.Add(BlockTypeParser.Create(
                typeValue,
                textValue.Trim(),
                level >= 0 ? BlockTypeParser.ParseLevel(data.Cell(row, level)) : null,
                styleValue));
        }
        return blocks;
    }

    private static List<Block> MapRecords(TabularData data, ImportOptions options, List<string> warnings)
    {
        int headingCol = 0;
        if (options.HeadingColumn is not null)
        {
            headingCol = data.IndexOf(options.HeadingColumn);
            if (headingCol < 0)
            {
                warnings.Add($"Heading column '{options.HeadingColumn}' not found in '{data.Name}'; using '{data.Headers[0]}'.");
                headingCol = 0;
            }
        }

        int groupCol = -1;
        if (options.GroupByColumn is not null)
        {
            groupCol = data.IndexOf(options.GroupByColumn);
            if (groupCol < 0)
                warnings.Add($"Group-by column '{options.GroupByColumn}' not found in '{data.Name}'; records will not be grouped.");
        }

        int recordLevel = Math.Clamp(options.RecordHeadingLevel ?? (groupCol >= 0 ? 2 : 1), 1, 6);
        int groupLevel = Math.Max(1, recordLevel - 1);
        var fieldColumns = Enumerable.Range(0, data.Headers.Count)
            .Where(i => i != headingCol && i != groupCol && !options.ExcludedColumns.Contains(data.Headers[i]))
            .ToList();

        var blocks = new List<Block>();
        string? currentGroup = null;
        foreach (var row in data.Rows)
        {
            if (groupCol >= 0)
            {
                var group = data.Cell(row, groupCol).Trim();
                if (group.Length > 0 && group != currentGroup)
                {
                    blocks.Add(Block.Heading(groupLevel, group));
                    currentGroup = group;
                }
            }

            var heading = data.Cell(row, headingCol).Trim();
            if (heading.Length > 0) blocks.Add(Block.Heading(recordLevel, heading));

            foreach (var col in fieldColumns)
            {
                var value = data.Cell(row, col).Replace("\r\n", "\n").Trim();
                if (value.Length == 0) continue;
                var paragraphs = BlankLine().Split(value).Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
                for (int i = 0; i < paragraphs.Count; i++)
                {
                    var text = paragraphs[i];
                    if (options.IncludeFieldLabels && i == 0)
                        text = $"**{InlineMarkup.Escape(data.Headers[col])}:** {text}";
                    blocks.Add(Block.Paragraph(text));
                }
            }
        }
        return blocks;
    }
}
