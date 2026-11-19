using IDDStruct.Core.Export;
using IDDStruct.Core.Import;
using IDDStruct.Core.Model;
using IDDStruct.Core.Serialization;
using IDDStruct.Idml;

return Cli.Run(args);

internal static class Cli
{
    private const string Usage = """
        IDDStruct – convert structured data into InDesign (IDML) documents.

        Usage:
          iddstruct convert <input> [-o <output.idml>] [options]
          iddstruct outline <input> [options]
          iddstruct project <input> [-o <output.iddstruct>] [options]
          iddstruct formats

        Inputs: .csv .tsv .txt .xlsx .xlsm .json .xml .iddstruct

        Import options:
          --heading-column <name>   Column used for record headings (default: first column)
          --group-by <name>         Group records under level-1 headings by this column
          --record-level <n>        Heading level for records
          --no-labels               Don't prefix field values with "Column:" labels
          --exclude <a,b,c>         Columns to leave out
          --record-mode             Treat type/text columns as ordinary data
          --delimiter <char>        CSV delimiter (default: auto-detect; use 'tab' for tab)
          --sheet <name>            Excel worksheet (default: first)
          --all-sheets              Import every worksheet, one level-1 heading per sheet

        Layout options:
          --page <A4|A5|A3|Letter|Legal>   Page size (default A4)
          --columns <n>                    Text columns per page (default 1)
          --pages <n>                      Fixed page count (default: estimated)
          --body-font <family>             Body font (default Minion Pro)
          --heading-font <family>          Heading font (default Myriad Pro)
          --title <text>                   Document title
        """;

    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
            var command = args[0].ToLowerInvariant();
            if (command == "formats")
            {
                foreach (var importer in DocumentImporter.Importers)
                    Console.WriteLine($"{importer.DisplayName,-16} {string.Join(" ", importer.Extensions)}");
                Console.WriteLine($"{"IDDStruct project",-16} {ProjectSerializer.FileExtension}");
                return 0;
            }

            var parsed = ParsedArgs.Parse(args.Skip(1).ToArray());
            if (parsed.Input is null) throw new ArgumentException("No input file given.");

            var result = DocumentImporter.Import(parsed.Input, parsed.Import);
            var doc = result.Document;
            parsed.ApplyLayout(doc);
            foreach (var warning in result.Warnings) Console.Error.WriteLine($"warning: {warning}");

            switch (command)
            {
                case "outline":
                    PrintOutline(doc);
                    return 0;
                case "project":
                {
                    var output = parsed.Output ?? Path.ChangeExtension(parsed.Input, ProjectSerializer.FileExtension);
                    ProjectSerializer.Save(doc, output);
                    Console.WriteLine($"Saved {doc.Blocks.Count} blocks to {output}");
                    return 0;
                }
                case "convert":
                {
                    var output = parsed.Output ?? Path.ChangeExtension(parsed.Input, ".idml");
                    var exporter = new IdmlExporter(new IdmlExportOptions { PageCount = parsed.Pages });
                    var report = exporter.ExportToFile(doc, output);
                    foreach (var warning in report.Warnings) Console.Error.WriteLine($"warning: {warning}");
                    Console.WriteLine($"Wrote {output} ({doc.Blocks.Count} blocks, {report.PageCount} pages)");
                    return 0;
                }
                default:
                    throw new ArgumentException($"Unknown command '{args[0]}'.");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or InvalidDataException
                                       or System.Text.Json.JsonException or System.Xml.XmlException)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
    }

    private static void PrintOutline(StructuredDocument doc)
    {
        Console.WriteLine($"# {doc.Title}");
        foreach (var block in doc.Blocks)
        {
            var text = block.PlainText.Replace('\n', ' ');
            if (text.Length > 90) text = text[..87] + "...";
            var line = block.Kind switch
            {
                BlockKind.Heading => $"{new string(' ', (block.Level - 1) * 2)}H{block.Level} {text}",
                BlockKind.BulletItem => $"{new string(' ', block.Level * 2)}• {text}",
                BlockKind.NumberedItem => $"{new string(' ', block.Level * 2)}#. {text}",
                BlockKind.Table => $"    [table {block.Table?.Rows.Count ?? 0}×{block.Table?.ColumnCount ?? 0}]",
                BlockKind.PageBreak => "    ---- page break ----",
                _ => $"    ¶ {text}",
            };
            var style = block.Style is null ? "" : $"   [{block.Style}]";
            Console.WriteLine(line + style);
        }
    }

    private sealed class ParsedArgs
    {
        public string? Input;
        public string? Output;
        public int? Pages;
        public string? PageSize;
        public int? Columns;
        public string? BodyFont;
        public string? HeadingFont;
        public string? Title;
        public ImportOptions Import { get; } = new();

        public static ParsedArgs Parse(string[] args)
        {
            var p = new ParsedArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Option {args[i]} needs a value.");
                int IntValue() { var name = args[i]; return int.TryParse(Value(), out var n) && n > 0 ? n : throw new ArgumentException($"Option {name} needs a positive whole number."); }

                switch (args[i])
                {
                    case "-o" or "--output": p.Output = Value(); break;
                    case "--heading-column": p.Import.HeadingColumn = Value(); break;
                    case "--group-by": p.Import.GroupByColumn = Value(); break;
                    case "--record-level": p.Import.RecordHeadingLevel = IntValue(); break;
                    case "--no-labels": p.Import.IncludeFieldLabels = false; p.Import.LabelScalarFields = false; break;
                    case "--exclude":
                        foreach (var c in Value().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            p.Import.ExcludedColumns.Add(c);
                        break;
                    case "--record-mode": p.Import.ForceRecordMode = true; break;
                    case "--delimiter":
                        var d = Value();
                        p.Import.CsvDelimiter = d.Equals("tab", StringComparison.OrdinalIgnoreCase) || d == "\\t" ? '\t'
                            : d.Length == 1 ? d[0] : throw new ArgumentException("Delimiter must be a single character or 'tab'.");
                        break;
                    case "--sheet": p.Import.SheetName = Value(); break;
                    case "--all-sheets": p.Import.AllSheets = true; break;
                    case "--page": p.PageSize = Value(); break;
                    case "--columns": p.Columns = IntValue(); break;
                    case "--pages": p.Pages = IntValue(); break;
                    case "--body-font": p.BodyFont = Value(); break;
                    case "--heading-font": p.HeadingFont = Value(); break;
                    case "--title": p.Title = Value(); break;
                    default:
                        if (args[i].StartsWith('-')) throw new ArgumentException($"Unknown option '{args[i]}'.");
                        if (p.Input is not null) throw new ArgumentException($"Unexpected argument '{args[i]}'.");
                        p.Input = args[i];
                        break;
                }
            }
            return p;
        }

        public void ApplyLayout(StructuredDocument doc)
        {
            if (PageSize is not null)
            {
                var preset = PageSetup.FromPreset(PageSize);
                doc.Page.Width = preset.Width;
                doc.Page.Height = preset.Height;
            }
            if (Columns is not null) doc.Page.Columns = Columns.Value;
            if (BodyFont is not null || HeadingFont is not null)
                doc.Styles = IDDStruct.Core.Styles.StyleSheet.CreateDefault(BodyFont ?? "Minion Pro", HeadingFont ?? "Myriad Pro");
            if (Title is not null) doc.Title = Title;
        }
    }
}
