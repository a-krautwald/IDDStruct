using IDDStruct.Core.Model;
using IDDStruct.Core.Styles;

namespace IDDStruct.Idml;

/// <summary>
/// IDML carries no layout engine, so the exporter must decide up front how many pages of
/// threaded text frames to create. This gives a deliberately generous estimate from text length
/// and style sizes; InDesign's Smart Text Reflow (enabled in the export) then adds or removes
/// pages when the text is next edited.
/// </summary>
public static class PageEstimator
{
    public static int Estimate(StructuredDocument doc, double safetyFactor = 1.15)
    {
        var page = doc.Page;
        int columns = Math.Max(1, page.Columns);
        double columnWidth = (page.TextAreaWidth - page.ColumnGutter * (columns - 1)) / columns;
        double frameHeight = page.TextAreaHeight;
        if (columnWidth <= 0 || frameHeight <= 0) return 1;

        double pages = 0, usedHeight = 0;
        void NewPage() { pages++; usedHeight = 0; }

        foreach (var block in doc.Blocks)
        {
            if (block.Kind == BlockKind.PageBreak)
            {
                if (usedHeight > 0) NewPage();
                continue;
            }

            double height;
            if (block.Kind == BlockKind.Table && block.Table is not null)
            {
                var style = doc.Styles.FindParagraphStyle(StyleSheet.TableBodyStyle);
                double size = style?.PointSize ?? 9, leading = style?.Leading ?? size * 1.2;
                int cols = Math.Max(1, block.Table.ColumnCount);
                double cellChars = Math.Max(1, (columnWidth / cols - 6) / (size * 0.5));
                height = block.Table.Rows.Sum(r =>
                    Math.Max(1, r.Select(c => Math.Ceiling(InlineMarkup.ToPlainText(c).Length / cellChars)).DefaultIfEmpty(1).Max()) * leading + 4);
                height += 6;
            }
            else
            {
                var style = doc.Styles.FindParagraphStyle(doc.Styles.ResolveParagraphStyle(block));
                double size = style?.PointSize ?? 10.5, leading = style?.Leading ?? size * 1.2;
                double indent = Math.Max(0, style?.LeftIndent ?? 0);
                // Average glyph width is roughly half the point size for Latin text faces.
                double charsPerLine = Math.Max(1, (columnWidth - indent) / (size * 0.5));
                int softBreaks = block.Text.Count(c => c == '\n');
                double lines = Math.Max(1, Math.Ceiling(block.PlainText.Length / charsPerLine)) + softBreaks;
                height = lines * leading + (style?.SpaceBefore ?? 0) + (style?.SpaceAfter ?? 0);
            }

            usedHeight += height;
            while (usedHeight > frameHeight * columns)
            {
                double overflow = usedHeight - frameHeight * columns;
                NewPage();
                usedHeight = overflow;
            }
        }
        if (usedHeight > 0 || pages == 0) pages++;
        return Math.Max(1, (int)Math.Round(pages * safetyFactor, MidpointRounding.AwayFromZero));
    }
}
