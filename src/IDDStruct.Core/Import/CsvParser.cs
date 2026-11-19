using System.Text;

namespace IDDStruct.Core.Import;

/// <summary>RFC 4180 CSV parser with delimiter auto-detection.</summary>
public static class CsvParser
{
    private static readonly char[] CandidateDelimiters = { ',', ';', '\t', '|' };

    /// <summary>Picks the candidate delimiter occurring most often (outside quotes) in the first line.</summary>
    public static char DetectDelimiter(string text)
    {
        var counts = new Dictionary<char, int>();
        bool inQuotes = false;
        foreach (char c in text)
        {
            if (c == '"') inQuotes = !inQuotes;
            else if (!inQuotes && (c == '\n' || c == '\r')) break;
            else if (!inQuotes && Array.IndexOf(CandidateDelimiters, c) >= 0)
                counts[c] = counts.GetValueOrDefault(c) + 1;
        }
        return counts.Count == 0 ? ',' : counts.OrderByDescending(kv => kv.Value).First().Key;
    }

    public static List<List<string>> Parse(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        bool fieldStarted = false;

        void EndField()
        {
            row.Add(field.ToString());
            field.Clear();
            fieldStarted = false;
        }

        void EndRow()
        {
            EndField();
            rows.Add(row);
            row = new List<string>();
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }

            if (c == '"' && !fieldStarted && field.Length == 0) { inQuotes = true; fieldStarted = true; }
            else if (c == delimiter) EndField();
            else if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                EndRow();
            }
            else if (c == '\n') EndRow();
            else { field.Append(c); fieldStarted = true; }
        }

        if (field.Length > 0 || row.Count > 0 || fieldStarted) EndRow();
        return rows;
    }
}
