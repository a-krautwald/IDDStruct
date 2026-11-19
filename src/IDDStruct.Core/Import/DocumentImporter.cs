using IDDStruct.Core.Serialization;

namespace IDDStruct.Core.Import;

/// <summary>Chooses an importer by file extension.</summary>
public static class DocumentImporter
{
    public static IReadOnlyList<IDataImporter> Importers { get; } = new IDataImporter[]
    {
        new CsvImporter(),
        new XlsxImporter(),
        new JsonImporter(),
        new XmlImporter(),
    };

    public static IEnumerable<string> SupportedExtensions =>
        Importers.SelectMany(i => i.Extensions).Append(ProjectSerializer.FileExtension);

    public static IDataImporter? FindImporter(string path)
    {
        var ext = Path.GetExtension(path);
        return Importers.FirstOrDefault(i => i.Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase));
    }

    public static bool IsTabular(string path) => FindImporter(path) is CsvImporter or XlsxImporter;

    public static ImportResult Import(string path, ImportOptions? options = null)
    {
        options ??= new ImportOptions();
        if (string.Equals(Path.GetExtension(path), ProjectSerializer.FileExtension, StringComparison.OrdinalIgnoreCase))
            return new ImportResult(ProjectSerializer.Load(path));

        var importer = FindImporter(path)
            ?? throw new NotSupportedException($"Unsupported file type '{Path.GetExtension(path)}'. Supported: {string.Join(", ", SupportedExtensions)}.");
        using var stream = File.OpenRead(path);
        return importer.Import(stream, Path.GetFileName(path), options);
    }

    /// <summary>Reads only the column headers of a tabular file (for import option pickers).</summary>
    public static IReadOnlyList<string> ReadHeaders(string path, ImportOptions? options = null)
    {
        options ??= new ImportOptions();
        using var stream = File.OpenRead(path);
        return FindImporter(path) switch
        {
            CsvImporter csv => csv.ReadTable(stream, Path.GetFileName(path), options).Headers,
            XlsxImporter xlsx => xlsx.ReadTables(stream, options).FirstOrDefault()?.Headers ?? (IReadOnlyList<string>)Array.Empty<string>(),
            _ => Array.Empty<string>(),
        };
    }
}
