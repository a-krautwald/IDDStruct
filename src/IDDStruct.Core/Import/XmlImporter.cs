using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using IDDStruct.Core.Import.Mapping;
using IDDStruct.Core.Model;

namespace IDDStruct.Core.Import;

public sealed partial class XmlImporter : IDataImporter
{
    public string DisplayName => "XML";
    public IReadOnlyList<string> Extensions { get; } = new[] { ".xml" };

    private static readonly HashSet<string> BoldTags = new(StringComparer.OrdinalIgnoreCase) { "b", "strong", "bold" };
    private static readonly HashSet<string> ItalicTags = new(StringComparer.OrdinalIgnoreCase) { "i", "em", "emphasis", "italic", "cite" };
    private static readonly HashSet<string> InlineTags = new(StringComparer.OrdinalIgnoreCase)
        { "b", "strong", "bold", "i", "em", "emphasis", "italic", "cite", "span", "a", "link", "code", "u", "sup", "sub", "small", "br", "abbr", "q" };

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    public static DataNode ToDataNode(XElement element)
    {
        var name = element.Name.LocalName;
        bool inlineOnly = element.Nodes().All(n => n is XText or XComment || n is XElement e && InlineTags.Contains(e.Name.LocalName) && IsInlineOnly(e));
        DataNode node;
        if (inlineOnly)
        {
            node = new DataNode(name, DataNodeKind.Scalar, InlineToMarkup(element).Trim());
        }
        else
        {
            node = new DataNode(name, DataNodeKind.Object);
            foreach (var child in element.Nodes())
            {
                if (child is XElement e) node.Children.Add(ToDataNode(e));
                else if (child is XText t)
                {
                    var text = Normalise(t.Value).Trim();
                    if (text.Length > 0) node.Children.Add(new DataNode("#text", DataNodeKind.Scalar, InlineMarkup.Escape(text)));
                }
            }
        }
        foreach (var attr in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
            node.Attributes[attr.Name.LocalName] = attr.Value;
        return node;
    }

    private static bool IsInlineOnly(XElement e) =>
        e.Nodes().All(n => n is XText or XComment || n is XElement c && InlineTags.Contains(c.Name.LocalName) && IsInlineOnly(c));

    private static string Normalise(string text) => Whitespace().Replace(text, " ");

    private static string InlineToMarkup(XElement element)
    {
        var sb = new StringBuilder();
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText t:
                    sb.Append(InlineMarkup.Escape(Normalise(t.Value)));
                    break;
                case XElement e when e.Name.LocalName.Equals("br", StringComparison.OrdinalIgnoreCase):
                    sb.Append('\n');
                    break;
                case XElement e:
                    var inner = InlineToMarkup(e);
                    var marker = BoldTags.Contains(e.Name.LocalName) ? "**" : ItalicTags.Contains(e.Name.LocalName) ? "*" : "";
                    if (marker.Length > 0 && inner.Trim().Length > 0)
                    {
                        int lead = inner.Length - inner.TrimStart().Length, trail = inner.Length - inner.TrimEnd().Length;
                        sb.Append(inner, 0, lead).Append(marker).Append(inner.Trim()).Append(marker).Append(inner, inner.Length - trail, trail);
                    }
                    else sb.Append(inner);
                    break;
            }
        }
        // Tidy spaces around soft line breaks.
        return string.Join("\n", sb.ToString().Split('\n').Select(l => l.Trim()));
    }

    public ImportResult Import(Stream source, string sourceName, ImportOptions options)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        using var reader = XmlReader.Create(source, settings);
        var doc = XDocument.Load(reader);
        if (doc.Root is null) throw new InvalidDataException($"'{sourceName}' has no root element.");

        var root = ToDataNode(doc.Root);
        var blocks = HierarchyMapper.Map(root, options, out var title);
        var result = new ImportResult(new StructuredDocument
        {
            Title = title ?? doc.Root.Attribute("title")?.Value ?? Path.GetFileNameWithoutExtension(sourceName),
        });
        result.Document.Blocks.AddRange(blocks);
        if (blocks.Count == 0) result.Warnings.Add($"No content found in '{sourceName}'.");
        return result;
    }
}
