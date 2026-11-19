using System.Globalization;
using System.Text.Json;
using IDDStruct.Core.Import.Mapping;
using IDDStruct.Core.Model;

namespace IDDStruct.Core.Import;

public sealed class JsonImporter : IDataImporter
{
    public string DisplayName => "JSON";
    public IReadOnlyList<string> Extensions { get; } = new[] { ".json" };

    public static DataNode ToDataNode(JsonElement element, string name)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var node = new DataNode(name, DataNodeKind.Object);
                foreach (var prop in element.EnumerateObject())
                    if (prop.Value.ValueKind != JsonValueKind.Null)
                        node.Children.Add(ToDataNode(prop.Value, prop.Name));
                return node;
            }
            case JsonValueKind.Array:
            {
                var node = new DataNode(name, DataNodeKind.Array);
                foreach (var item in element.EnumerateArray())
                    if (item.ValueKind != JsonValueKind.Null)
                        node.Children.Add(ToDataNode(item, name));
                return node;
            }
            case JsonValueKind.String:
                return new DataNode(name, DataNodeKind.Scalar, element.GetString());
            case JsonValueKind.Number:
                return new DataNode(name, DataNodeKind.Scalar, element.GetRawText());
            case JsonValueKind.True:
            case JsonValueKind.False:
                return new DataNode(name, DataNodeKind.Scalar, element.GetBoolean() ? "true" : "false");
            default:
                return new DataNode(name, DataNodeKind.Scalar, element.ToString());
        }
    }

    public ImportResult Import(Stream source, string sourceName, ImportOptions options)
    {
        using var json = JsonDocument.Parse(source, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });
        var root = ToDataNode(json.RootElement, "root");
        var blocks = HierarchyMapper.Map(root, options, out var title);
        var result = new ImportResult(new StructuredDocument
        {
            Title = title ?? Path.GetFileNameWithoutExtension(sourceName),
        });
        result.Document.Blocks.AddRange(blocks);
        if (blocks.Count == 0) result.Warnings.Add($"No content found in '{sourceName}'.");
        return result;
    }
}
