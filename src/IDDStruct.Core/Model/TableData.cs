namespace IDDStruct.Core.Model;

/// <summary>A simple rectangular table. Cell text uses the same inline markup as blocks.</summary>
public sealed class TableData
{
    public List<List<string>> Rows { get; set; } = new();

    /// <summary>Number of leading rows treated as header rows.</summary>
    public int HeaderRows { get; set; } = 1;

    public int ColumnCount => Rows.Count == 0 ? 0 : Rows.Max(r => r.Count);

    public TableData Clone() => new()
    {
        HeaderRows = HeaderRows,
        Rows = Rows.Select(r => r.ToList()).ToList(),
    };

    /// <summary>Parses tab- or pipe-separated lines into a table (one row per line).</summary>
    public static TableData FromDelimitedText(string text, int headerRows = 1)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n')
            .Where(l => l.Trim().Length > 0)
            .ToList();
        char sep = lines.Any(l => l.Contains('\t')) ? '\t' : '|';
        var rows = lines
            .Select(l => l.Trim().Trim('|').Split(sep).Select(c => c.Trim()).ToList())
            .ToList();
        return new TableData { Rows = rows, HeaderRows = headerRows };
    }

    /// <summary>Serialises the table as tab-separated lines (inverse of <see cref="FromDelimitedText"/>).</summary>
    public string ToDelimitedText() =>
        string.Join("\n", Rows.Select(r => string.Join("\t", r)));
}
