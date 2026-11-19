using IDDStruct.Core.Import.Mapping;
using IDDStruct.Core.Model;

namespace IDDStruct.Core.Import;

public sealed class CsvImporter : IDataImporter
{
    public string DisplayName => "CSV / TSV";
    public IReadOnlyList<string> Extensions { get; } = new[] { ".csv", ".tsv", ".tab", ".txt" };

    public TabularData ReadTable(Stream source, string sourceName, ImportOptions options, List<string>? warnings = null)
    {
        var text = TextDecoding.Decode(TextDecoding.ReadAll(source), out bool fallback);
        if (fallback)
            warnings?.Add($"'{sourceName}' is not valid UTF-8; read as Latin-1 (Windows Western). Check special characters.");
        char delimiter = options.CsvDelimiter
            ?? (Path.GetExtension(sourceName).ToLowerInvariant() is ".tsv" or ".tab" ? '\t' : CsvParser.DetectDelimiter(text));
        return TabularData.FromRows(CsvParser.Parse(text, delimiter), Path.GetFileNameWithoutExtension(sourceName));
    }

    public ImportResult Import(Stream source, string sourceName, ImportOptions options)
    {
        var warnings = new List<string>();
        var table = ReadTable(source, sourceName, options, warnings);
        var result = new ImportResult(new StructuredDocument { Title = table.Name });
        result.Warnings.AddRange(warnings);
        result.Document.Blocks.AddRange(TabularMapper.Map(table, options, result.Warnings));
        if (table.Rows.Count == 0) result.Warnings.Add($"'{sourceName}' contains no data rows.");
        return result;
    }
}
