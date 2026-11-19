using System.Text;
using IDDStruct.Core.Model;

namespace IDDStruct.Core.Import.Mapping;

/// <summary>
/// Maps a JSON/XML <see cref="DataNode"/> tree to long-form structure.
/// Recognises HTML/DocBook-like names (h1–h6, p, ul/ol/li, table/tr/td, section/title …) and
/// objects carrying a <c>type</c> property; anything else is mapped generically: objects become
/// sections (headed by their title/name), arrays of values become bullet lists, other values paragraphs.
/// </summary>
public sealed class HierarchyMapper
{
    private static readonly HashSet<string> TitleNames = new(StringComparer.OrdinalIgnoreCase)
        { "title", "name", "heading", "header", "label", "caption" };
    private static readonly HashSet<string> TextNames = new(StringComparer.OrdinalIgnoreCase)
        { "p", "para", "paragraph", "text", "body", "content", "description", "summary", "abstract", "intro", "introduction", "#text" };
    private static readonly HashSet<string> ContainerNames = new(StringComparer.OrdinalIgnoreCase)
        { "root", "document", "doc", "book", "body", "content", "contents", "section", "sections", "chapter", "chapters",
          "article", "articles", "part", "parts", "div", "blocks", "items", "entries", "records", "data", "sect1", "sect2", "sect3" };
    private static readonly HashSet<string> ListNames = new(StringComparer.OrdinalIgnoreCase)
        { "ul", "ol", "list", "bullets", "steps", "itemizedlist", "orderedlist" };
    private static readonly HashSet<string> OrderedListNames = new(StringComparer.OrdinalIgnoreCase)
        { "ol", "steps", "orderedlist" };

    private readonly ImportOptions _options;
    private readonly List<Block> _blocks = new();

    private HierarchyMapper(ImportOptions options) => _options = options;

    public static List<Block> Map(DataNode root, ImportOptions options, out string? title)
    {
        var mapper = new HierarchyMapper(options);
        title = null;
        if (root.Kind == DataNodeKind.Object)
        {
            // Treat a root "title" as the document title. If the root also carries an explicit
            // block list, the title is metadata only; otherwise it is also the level-1 heading.
            var titleNode = root.Children.FirstOrDefault(c => c.IsScalar && TitleNames.Contains(c.Name));
            title = titleNode is null ? null : InlineMarkup.ToPlainText(titleNode.Value);
            bool hasBlockList = root.Children.Any(c => c.Kind == DataNodeKind.Array && string.Equals(c.Name, "blocks", StringComparison.OrdinalIgnoreCase));
            mapper.MapObject(root, 1, isRoot: true, emitTitle: !hasBlockList);
        }
        else mapper.MapNode(root, 1);
        return mapper._blocks;
    }

    private void Add(Block block, DataNode source)
    {
        var style = source.Attributes.GetValueOrDefault("style") ?? source.Attributes.GetValueOrDefault("class");
        if (!string.IsNullOrWhiteSpace(style) && block.Kind != BlockKind.PageBreak) block.Style = style.Trim();
        _blocks.Add(block);
    }

    private void MapNode(DataNode node, int level)
    {
        var name = node.Name;
        var lower = name.ToLowerInvariant();

        if (node.Kind == DataNodeKind.Object && node.GetText("type") is { } type && !IsListNode(node) && lower != "table")
        {
            MapTypedObject(node, type);
            return;
        }

        if (lower.Length == 2 && lower[0] == 'h' && char.IsAsciiDigit(lower[1]) && lower[1] is >= '1' and <= '6')
        {
            Add(Block.Heading(lower[1] - '0', TextOf(node)), node);
            return;
        }

        switch (lower)
        {
            case "heading" or "header" or "title" when node.IsScalar:
                Add(Block.Heading(BlockTypeParser.ParseLevel(node.Attributes.GetValueOrDefault("level")) ?? level, node.Value ?? ""), node);
                return;
            case "pagebreak" or "page-break" or "page_break":
                _blocks.Add(Block.PageBreak());
                return;
            case "quote" or "blockquote":
                Add(Block.Paragraph(TextOf(node), "Quote"), node);
                return;
            case "table" or "informaltable":
                MapTable(node);
                return;
            case "li" or "item" or "listitem" when node.IsScalar:
                Add(Block.Bullet(node.Value ?? ""), node);
                return;
        }

        if (IsListNode(node) && node.Kind != DataNodeKind.Scalar)
        {
            MapList(node, OrderedListNames.Contains(name) || IsTruthy(node.Attributes.GetValueOrDefault("ordered"))
                || string.Equals(node.Attributes.GetValueOrDefault("type"), "ordered", StringComparison.OrdinalIgnoreCase), 1);
            return;
        }

        switch (node.Kind)
        {
            case DataNodeKind.Scalar:
                if (string.IsNullOrWhiteSpace(node.Value)) return;
                bool label = _options.LabelScalarFields && !TextNames.Contains(name) && !string.IsNullOrEmpty(name);
                Add(Block.Paragraph(label ? $"**{InlineMarkup.Escape(Humanise(name))}:** {node.Value}" : node.Value!), node);
                return;
            case DataNodeKind.Array:
                if (node.Children.Count > 0 && node.Children.All(c => c.IsScalar))
                {
                    foreach (var item in node.Children.Where(c => !string.IsNullOrWhiteSpace(c.Value)))
                        _blocks.Add(Block.Bullet(item.Value!));
                }
                else
                {
                    foreach (var item in node.Children)
                    {
                        if (item.Kind == DataNodeKind.Object && item.GetText("type") is null)
                            MapObject(item, level, isRoot: false, emitTitle: true, isArrayItem: true);
                        else MapNode(item, level);
                    }
                }
                return;
            case DataNodeKind.Object:
                MapObject(node, level, isRoot: false, emitTitle: true);
                return;
        }
    }

    private void MapObject(DataNode node, int level, bool isRoot, bool emitTitle, bool isArrayItem = false)
    {
        var titleNode = node.Children.FirstOrDefault(c => c.IsScalar && TitleNames.Contains(c.Name));
        var titleText = titleNode?.Value ?? node.Attributes.GetValueOrDefault("title");
        int childLevel = level;

        if (emitTitle && !string.IsNullOrWhiteSpace(titleText))
        {
            Add(Block.Heading(level, titleText), node);
            childLevel = level + 1;
        }
        else if (!isRoot && !isArrayItem && titleNode is null && !ContainerNames.Contains(node.Name))
        {
            Add(Block.Heading(level, Humanise(node.Name)), node);
            childLevel = level + 1;
        }
        else if (!isRoot && ContainerNames.Contains(node.Name) && titleText is null)
        {
            childLevel = level; // pure wrapper: no heading, same depth
        }

        foreach (var child in node.Children)
        {
            if (ReferenceEquals(child, titleNode)) continue;
            MapNode(child, Math.Min(childLevel, 6));
        }
    }

    private void MapTypedObject(DataNode node, string type)
    {
        var text = node.GetText("text", "content", "value", "body") ?? "";
        var level = BlockTypeParser.ParseLevel(node.GetText("level", "depth"));
        var style = node.GetText("style");
        var lowerType = type.Trim().ToLowerInvariant();

        if (lowerType is "table" && node.GetChild("rows") is { } rows)
        {
            MapTable(rows, ParseInt(node.GetText("headerRows")) ?? 1);
            return;
        }
        if (lowerType is "list" or "ul" or "ol" or "bullets" or "numbered" && node.GetChild("items") is { } items)
        {
            MapList(items, lowerType is "ol" or "numbered" || IsTruthy(node.GetText("ordered")), level ?? 1);
            return;
        }
        _blocks.Add(BlockTypeParser.Create(type, text, level, style));
    }

    private void MapList(DataNode list, bool ordered, int depth)
    {
        foreach (var item in list.Children)
        {
            if (item.IsScalar)
            {
                if (string.IsNullOrWhiteSpace(item.Value)) continue;
                Add(ordered ? Block.Numbered(item.Value!, depth) : Block.Bullet(item.Value!, depth), item);
                continue;
            }

            // A complex list item: its own text, then any nested lists one level deeper.
            var textParts = item.Children.Where(c => c.IsScalar && !string.IsNullOrWhiteSpace(c.Value)).Select(c => c.Value!).ToList();
            if (item.Kind == DataNodeKind.Object && item.GetText("type") is null && textParts.Count > 0)
                Add(ordered ? Block.Numbered(string.Join(" ", textParts), depth) : Block.Bullet(string.Join(" ", textParts), depth), item);
            foreach (var nested in item.Children.Where(c => !c.IsScalar))
            {
                if (IsListNode(nested) || nested.Kind == DataNodeKind.Array)
                    MapList(nested, OrderedListNames.Contains(nested.Name) || ordered && nested.Kind == DataNodeKind.Array, Math.Min(depth + 1, 3));
                else MapNode(nested, depth);
            }
        }
    }

    private void MapTable(DataNode table, int headerRows = -1)
    {
        var rows = new List<List<string>>();
        int headerCount = 0;
        void Collect(DataNode node, bool inHead)
        {
            foreach (var child in node.Children)
            {
                var n = child.Name.ToLowerInvariant();
                if (n is "thead") Collect(child, true);
                else if (n is "tbody" or "tfoot" or "tgroup") Collect(child, false);
                else if (child.IsScalar) rows.Add(new List<string> { child.Value ?? "" });
                else
                {
                    // A row: either <tr>/<row> of cells or a JSON array/object of values.
                    var cells = child.Children.Select(c => c.IsScalar ? c.Value ?? "" : FlattenText(c)).ToList();
                    bool isHeader = inHead || child.Children.Count > 0 && child.Children.All(c => c.Name.Equals("th", StringComparison.OrdinalIgnoreCase));
                    if (isHeader && rows.Count == headerCount) headerCount++;
                    rows.Add(cells);
                }
            }
        }
        Collect(table, false);
        if (rows.Count == 0) return;

        // JSON array of objects: use property names as the header row.
        var firstRowNode = table.Children.FirstOrDefault();
        if (firstRowNode?.Kind == DataNodeKind.Object && table.Kind == DataNodeKind.Array)
        {
            rows.Insert(0, firstRowNode.Children.Select(c => Humanise(c.Name)).ToList());
            headerCount = 1;
        }

        Add(Block.FromTable(new TableData { Rows = rows, HeaderRows = headerRows >= 0 ? headerRows : headerCount }), table);
    }

    private static bool IsListNode(DataNode node) => ListNames.Contains(node.Name);

    private static string TextOf(DataNode node) => node.IsScalar ? node.Value ?? "" : FlattenText(node);

    private static string FlattenText(DataNode node) =>
        node.IsScalar ? node.Value ?? "" : string.Join(" ", node.Children.Select(FlattenText).Where(s => s.Length > 0));

    private static bool IsTruthy(string? value) =>
        value is not null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase));

    private static int? ParseInt(string? value) => int.TryParse(value, out var n) ? n : null;

    /// <summary>"first_name" / "firstName" / "first-name" → "First name".</summary>
    public static string Humanise(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c is '_' or '-' or '.') { sb.Append(' '); continue; }
            if (i > 0 && char.IsUpper(c) && char.IsLower(name[i - 1])) sb.Append(' ');
            sb.Append(sb.Length == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
        }
        return string.Join(" ", sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
