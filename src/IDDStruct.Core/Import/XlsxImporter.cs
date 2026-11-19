using ClosedXML.Excel;
using IDDStruct.Core.Import.Mapping;
using IDDStruct.Core.Model;

namespace IDDStruct.Core.Import;

public sealed class XlsxImporter : IDataImporter
{
    public string DisplayName => "Excel workbook";
    public IReadOnlyList<string> Extensions { get; } = new[] { ".xlsx", ".xlsm" };

    public IReadOnlyList<TabularData> ReadTables(Stream source, ImportOptions options, List<string>? warnings = null)
    {
        // ClosedXML needs a seekable stream.
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        buffer.Position = 0;
        using var workbook = new XLWorkbook(buffer);

        IEnumerable<IXLWorksheet> sheets;
        if (options.AllSheets) sheets = workbook.Worksheets;
        else if (options.SheetName is not null)
        {
            if (!workbook.TryGetWorksheet(options.SheetName, out var named))
                throw new InvalidDataException($"Worksheet '{options.SheetName}' not found. Sheets: {string.Join(", ", workbook.Worksheets.Select(w => w.Name))}.");
            sheets = new[] { named };
        }
        else sheets = workbook.Worksheets.Take(1);

        var tables = new List<TabularData>();
        foreach (var sheet in sheets)
        {
            var range = sheet.RangeUsed();
            if (range is null)
            {
                warnings?.Add($"Worksheet '{sheet.Name}' is empty.");
                continue;
            }
            int columns = range.ColumnCount();
            var rows = range.Rows().Select(r =>
                (IReadOnlyList<string>)Enumerable.Range(1, columns).Select(c => r.Cell(c).GetFormattedString()).ToList());
            tables.Add(TabularData.FromRows(rows, sheet.Name));
        }
        return tables;
    }

    public ImportResult Import(Stream source, string sourceName, ImportOptions options)
    {
        var warnings = new List<string>();
        var tables = ReadTables(source, options, warnings);
        var result = new ImportResult(new StructuredDocument { Title = Path.GetFileNameWithoutExtension(sourceName) });
        result.Warnings.AddRange(warnings);

        bool multi = options.AllSheets && tables.Count > 1;
        var sheetOptions = options;
        if (multi && options.RecordHeadingLevel is null)
        {
            // Sheet names take level 1, so push records (and groups) one level down.
            sheetOptions = Clone(options);
            sheetOptions.RecordHeadingLevel = options.GroupByColumn is null ? 2 : 3;
        }

        foreach (var table in tables)
        {
            var blocks = TabularMapper.Map(table, sheetOptions, result.Warnings);
            if (multi)
            {
                result.Document.Blocks.Add(Block.Heading(1, table.Name));
                if (TabularMapper.LooksLikeBlockList(table) && !options.ForceRecordMode)
                    foreach (var b in blocks.Where(b => b.Kind == BlockKind.Heading)) b.Level = Math.Min(b.Level + 1, 6);
            }
            result.Document.Blocks.AddRange(blocks);
        }
        return result;
    }

    private static ImportOptions Clone(ImportOptions o) => new()
    {
        HeadingColumn = o.HeadingColumn,
        GroupByColumn = o.GroupByColumn,
        RecordHeadingLevel = o.RecordHeadingLevel,
        IncludeFieldLabels = o.IncludeFieldLabels,
        ExcludedColumns = new HashSet<string>(o.ExcludedColumns, StringComparer.OrdinalIgnoreCase),
        ForceRecordMode = o.ForceRecordMode,
        CsvDelimiter = o.CsvDelimiter,
        SheetName = o.SheetName,
        AllSheets = o.AllSheets,
        LabelScalarFields = o.LabelScalarFields,
    };
}
