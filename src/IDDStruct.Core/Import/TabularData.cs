namespace IDDStruct.Core.Import;

/// <summary>Rows and columns read from CSV/TSV/XLSX, with a normalised header row.</summary>
public sealed class TabularData
{
    public string Name { get; set; } = "";
    public List<string> Headers { get; } = new();
    public List<List<string>> Rows { get; } = new();

    public int IndexOf(string? header) => header is null
        ? -1
        : Headers.FindIndex(h => string.Equals(h, header.Trim(), StringComparison.OrdinalIgnoreCase));

    public string Cell(List<string> row, int column) =>
        column >= 0 && column < row.Count ? row[column] : "";

    /// <summary>Builds tabular data from raw rows; the first non-empty row is the header row.</summary>
    public static TabularData FromRows(IEnumerable<IReadOnlyList<string>> rawRows, string name = "")
    {
        var data = new TabularData { Name = name };
        var rows = rawRows
            .Select(r => r.Select(c => c ?? "").ToList())
            .Where(r => r.Any(c => c.Trim().Length > 0))
            .ToList();
        if (rows.Count == 0) return data;

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int width = rows.Max(r => r.Count);
        for (int i = 0; i < width; i++)
        {
            var header = i < rows[0].Count ? rows[0][i].Trim() : "";
            if (header.Length == 0) header = $"Column {i + 1}";
            if (seen.TryGetValue(header, out var count))
            {
                seen[header] = count + 1;
                header = $"{header} ({count + 1})";
            }
            else seen[header] = 1;
            data.Headers.Add(header);
        }
        foreach (var row in rows.Skip(1))
        {
            while (row.Count < width) row.Add("");
            data.Rows.Add(row);
        }
        return data;
    }
}
