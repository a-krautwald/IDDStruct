namespace IDDStruct.Core.Import;

/// <summary>Options controlling how source data is mapped onto document structure.</summary>
public sealed class ImportOptions
{
    // ---- Tabular sources (CSV/TSV/XLSX) in record mode ----

    /// <summary>Column whose value becomes each record's heading. Defaults to the first column.</summary>
    public string? HeadingColumn { get; set; }

    /// <summary>Optional column used to group records under level-1 headings when its value changes.</summary>
    public string? GroupByColumn { get; set; }

    /// <summary>Heading level for each record. Defaults to 2 when grouping, otherwise 1.</summary>
    public int? RecordHeadingLevel { get; set; }

    /// <summary>Prefix each field paragraph with a bold "Column name:" label.</summary>
    public bool IncludeFieldLabels { get; set; } = true;

    /// <summary>Columns to leave out entirely.</summary>
    public HashSet<string> ExcludedColumns { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Force record mode even when the columns look like a block list (type/text).</summary>
    public bool ForceRecordMode { get; set; }

    // ---- CSV ----

    /// <summary>Field delimiter; auto-detected (comma, semicolon, tab, pipe) when null.</summary>
    public char? CsvDelimiter { get; set; }

    // ---- XLSX ----

    /// <summary>Worksheet to import. Defaults to the first sheet.</summary>
    public string? SheetName { get; set; }

    /// <summary>Import every worksheet, each under its own level-1 heading.</summary>
    public bool AllSheets { get; set; }

    // ---- Hierarchical sources (JSON/XML) ----

    /// <summary>Label scalar fields that are not obviously body text ("**Author:** Jane").</summary>
    public bool LabelScalarFields { get; set; } = true;
}
