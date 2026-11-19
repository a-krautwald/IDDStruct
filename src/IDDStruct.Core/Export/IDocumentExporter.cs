using IDDStruct.Core.Model;

namespace IDDStruct.Core.Export;

/// <summary>
/// Converts a <see cref="StructuredDocument"/> into an output format. The IDML exporter is the
/// first implementation; an exporter that drives an installed copy of InDesign can implement
/// the same interface later.
/// </summary>
public interface IDocumentExporter
{
    string DisplayName { get; }

    /// <summary>File extension including the dot, e.g. ".idml".</summary>
    string FileExtension { get; }

    ExportReport Export(StructuredDocument document, Stream output);
}

public sealed class ExportReport
{
    public int PageCount { get; init; }
    public List<string> Warnings { get; } = new();
}

public static class ExporterExtensions
{
    public static ExportReport ExportToFile(this IDocumentExporter exporter, StructuredDocument document, string path)
    {
        using var stream = File.Create(path);
        return exporter.Export(document, stream);
    }
}
