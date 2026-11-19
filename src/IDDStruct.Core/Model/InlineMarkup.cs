using System.Text;

namespace IDDStruct.Core.Model;

/// <summary>
/// Minimal inline markup: <c>**bold**</c>, <c>*italic*</c>, <c>***bold italic***</c>.
/// A backslash escapes the next character (<c>\*</c> for a literal asterisk).
/// An asterisk only opens formatting when followed by non-whitespace and only closes
/// when preceded by non-whitespace, so "5 * 3" stays literal.
/// </summary>
public static class InlineMarkup
{
    public static IReadOnlyList<TextRun> Parse(string? markup)
    {
        var runs = new List<TextRun>();
        if (string.IsNullOrEmpty(markup)) return runs;

        var sb = new StringBuilder();
        bool bold = false, italic = false;

        void Flush()
        {
            if (sb.Length == 0) return;
            var text = sb.ToString();
            sb.Clear();
            if (runs.Count > 0 && runs[^1].Bold == bold && runs[^1].Italic == italic)
                runs[^1] = runs[^1] with { Text = runs[^1].Text + text };
            else
                runs.Add(new TextRun(text, bold, italic));
        }

        for (int i = 0; i < markup.Length; i++)
        {
            char c = markup[i];
            if (c == '\\' && i + 1 < markup.Length && (markup[i + 1] == '*' || markup[i + 1] == '\\'))
            {
                sb.Append(markup[++i]);
                continue;
            }
            if (c != '*')
            {
                sb.Append(c);
                continue;
            }

            int n = 1;
            while (i + n < markup.Length && markup[i + n] == '*' && n < 3) n++;
            bool prevSolid = i > 0 && !char.IsWhiteSpace(markup[i - 1]);
            bool nextSolid = i + n < markup.Length && !char.IsWhiteSpace(markup[i + n]);

            bool handled = false;
            if (n == 3)
            {
                if (bold && italic && prevSolid) { Flush(); bold = italic = false; handled = true; }
                else if (!bold && !italic && nextSolid) { Flush(); bold = italic = true; handled = true; }
            }
            else if (n == 2)
            {
                if (bold && prevSolid) { Flush(); bold = false; handled = true; }
                else if (!bold && nextSolid) { Flush(); bold = true; handled = true; }
            }
            else
            {
                if (italic && prevSolid) { Flush(); italic = false; handled = true; }
                else if (!italic && nextSolid) { Flush(); italic = true; handled = true; }
            }

            if (!handled) sb.Append('*', n);
            i += n - 1;
        }
        Flush();
        return runs;
    }

    public static string ToPlainText(string? markup) =>
        string.Concat(Parse(markup).Select(r => r.Text));

    public static string Escape(string text) =>
        text.Replace("\\", "\\\\").Replace("*", "\\*");

    public static string ToMarkup(IEnumerable<TextRun> runs)
    {
        var sb = new StringBuilder();
        foreach (var run in runs)
        {
            var marker = run.Bold && run.Italic ? "***" : run.Bold ? "**" : run.Italic ? "*" : "";
            var text = Escape(run.Text);
            if (marker.Length == 0 || text.Trim().Length == 0)
            {
                sb.Append(text);
                continue;
            }
            // Keep surrounding whitespace outside the markers so they still parse.
            var core = text.Trim();
            int lead = text.Length - text.TrimStart().Length;
            int trail = text.Length - text.TrimEnd().Length;
            sb.Append(text, 0, lead).Append(marker).Append(core).Append(marker).Append(text, text.Length - trail, trail);
        }
        return sb.ToString();
    }
}
