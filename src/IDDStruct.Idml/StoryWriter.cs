using System.Xml;
using IDDStruct.Core.Model;
using IDDStruct.Core.Styles;
using static IDDStruct.Idml.IdmlFormat;

namespace IDDStruct.Idml;

/// <summary>Writes the single flowing story that holds all document content.</summary>
internal sealed class StoryWriter
{
    private readonly StructuredDocument _doc;
    private readonly IdGenerator _ids;
    private readonly double _columnWidth;

    public StoryWriter(StructuredDocument doc, IdGenerator ids, double columnWidth)
    {
        _doc = doc;
        _ids = ids;
        _columnWidth = columnWidth;
    }

    public void Write(XmlWriter w, string storyId)
    {
        StartPart(w, "Story");
        w.WriteStartElement("Story");
        Attr(w, "Self", storyId);
        Attr(w, "AppliedTOCStyle", "n");
        Attr(w, "TrackChanges", false);
        Attr(w, "StoryTitle", "$ID/");
        Attr(w, "AppliedNamedGrid", "n");

        w.WriteStartElement("StoryPreference");
        Attr(w, "OpticalMarginAlignment", false);
        Attr(w, "OpticalMarginSize", 12);
        Attr(w, "FrameType", "TextFrameType");
        Attr(w, "StoryOrientation", "Horizontal");
        Attr(w, "StoryDirection", "LeftToRightDirection");
        w.WriteEndElement();

        var blocks = _doc.Blocks;
        int lastContent = blocks.FindLastIndex(b => b.Kind != BlockKind.PageBreak);
        bool startOnNextPage = false;
        Block? previous = null;

        for (int i = 0; i <= lastContent; i++)
        {
            var block = blocks[i];
            if (block.Kind == BlockKind.PageBreak)
            {
                startOnNextPage = previous is not null;
                continue;
            }

            bool isLast = i == lastContent;
            if (block.Kind == BlockKind.Table && block.Table is { Rows.Count: > 0 } table)
                WriteTable(w, table, startOnNextPage, isLast);
            else
                WriteParagraph(w, block, previous, startOnNextPage, isLast);

            startOnNextPage = false;
            previous = block;
        }

        if (lastContent < 0)
        {
            // An empty story still needs one paragraph range.
            w.WriteStartElement("ParagraphStyleRange");
            Attr(w, "AppliedParagraphStyle", ParagraphStyleRef(StyleSheet.BodyStyle));
            w.WriteStartElement("CharacterStyleRange");
            Attr(w, "AppliedCharacterStyle", NoCharacterStyle);
            w.WriteEndElement();
            w.WriteEndElement();
        }

        w.WriteEndElement(); // Story
        EndPart(w);
    }

    private void WriteParagraph(XmlWriter w, Block block, Block? previous, bool startOnNextPage, bool isLast)
    {
        w.WriteStartElement("ParagraphStyleRange");
        Attr(w, "AppliedParagraphStyle", ParagraphStyleRef(_doc.Styles.ResolveParagraphStyle(block)));
        if (startOnNextPage) Attr(w, "StartParagraph", "NextPage");
        if (block.Kind == BlockKind.NumberedItem && RestartsNumbering(block, previous))
        {
            Attr(w, "NumberingContinue", false);
            Attr(w, "NumberingStartAt", 1);
        }
        WriteRuns(w, block.GetRuns(), appendBreak: !isLast);
        w.WriteEndElement();
    }

    /// <summary>A numbered list restarts unless it directly continues a numbered list (or a deeper nested item).</summary>
    private static bool RestartsNumbering(Block block, Block? previous) =>
        previous is null
        || previous.Kind is not (BlockKind.NumberedItem or BlockKind.BulletItem)
        || previous.Kind == BlockKind.BulletItem && previous.Level <= block.Level;

    private static void WriteRuns(XmlWriter w, IReadOnlyList<TextRun> runs, bool appendBreak)
    {
        if (runs.Count == 0)
        {
            w.WriteStartElement("CharacterStyleRange");
            Attr(w, "AppliedCharacterStyle", NoCharacterStyle);
            if (appendBreak) w.WriteElementString("Br", "");
            w.WriteEndElement();
            return;
        }

        for (int i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            w.WriteStartElement("CharacterStyleRange");
            Attr(w, "AppliedCharacterStyle", CharacterStyleRef(StyleSheet.ResolveCharacterStyle(run)));
            WriteContent(w, run.Text);
            if (appendBreak && i == runs.Count - 1) w.WriteElementString("Br", "");
            w.WriteEndElement();
        }
    }

    /// <summary>
    /// Writes text as &lt;Content&gt;. Line feeds inside a block become InDesign forced line breaks
    /// (U+2028); other control characters are dropped as they are not valid XML.
    /// </summary>
    private const char ForcedLineBreak = (char)0x2028;

    private static void WriteContent(XmlWriter w, string text)
    {
        var clean = new System.Text.StringBuilder(text.Length);
        foreach (var c in text.Replace("\r\n", "\n"))
        {
            if (c == '\n' || c == '\r') clean.Append(ForcedLineBreak);
            else if (c == '\t' || !char.IsControl(c)) clean.Append(c);
        }
        if (clean.Length > 0) w.WriteElementString("Content", clean.ToString());
    }

    private void WriteTable(XmlWriter w, TableData table, bool startOnNextPage, bool isLast)
    {
        int columns = Math.Max(1, table.ColumnCount);
        int rows = table.Rows.Count;
        int header = rows <= 1 ? 0 : Math.Clamp(table.HeaderRows, 0, rows - 1);
        string tableId = _ids.Next("t");
        double columnWidth = _columnWidth / columns;

        w.WriteStartElement("ParagraphStyleRange");
        Attr(w, "AppliedParagraphStyle", ParagraphStyleRef(StyleSheet.BodyStyle));
        if (startOnNextPage) Attr(w, "StartParagraph", "NextPage");
        w.WriteStartElement("CharacterStyleRange");
        Attr(w, "AppliedCharacterStyle", NoCharacterStyle);

        w.WriteStartElement("Table");
        Attr(w, "Self", tableId);
        Attr(w, "HeaderRowCount", header);
        Attr(w, "FooterRowCount", 0);
        Attr(w, "BodyRowCount", rows - header);
        Attr(w, "ColumnCount", columns);
        Attr(w, "AppliedTableStyle", "TableStyle/$ID/[Basic Table]");
        Attr(w, "TableDirection", "LeftToRightDirection");

        for (int r = 0; r < rows; r++)
        {
            w.WriteStartElement("Row");
            Attr(w, "Self", $"{tableId}Row{r}");
            Attr(w, "Name", r);
            Attr(w, "MinimumHeight", 14);
            Attr(w, "AutoGrow", true);
            w.WriteEndElement();
        }
        for (int c = 0; c < columns; c++)
        {
            w.WriteStartElement("Column");
            Attr(w, "Self", $"{tableId}Column{c}");
            Attr(w, "Name", c);
            Attr(w, "SingleColumnWidth", columnWidth);
            w.WriteEndElement();
        }

        int cellIndex = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                var text = c < table.Rows[r].Count ? table.Rows[r][c] : "";
                w.WriteStartElement("Cell");
                Attr(w, "Self", $"{tableId}i{cellIndex++}");
                Attr(w, "Name", $"{c}:{r}");
                Attr(w, "RowSpan", 1);
                Attr(w, "ColumnSpan", 1);
                Attr(w, "AppliedCellStyle", "CellStyle/$ID/[None]");
                Attr(w, "AppliedCellStylePriority", 0);
                w.WriteStartElement("ParagraphStyleRange");
                Attr(w, "AppliedParagraphStyle", ParagraphStyleRef(r < header ? StyleSheet.TableHeaderStyle : StyleSheet.TableBodyStyle));
                WriteRuns(w, InlineMarkup.Parse(text), appendBreak: false);
                w.WriteEndElement();
                w.WriteEndElement();
            }
        }

        w.WriteEndElement(); // Table
        if (!isLast) w.WriteElementString("Br", "");
        w.WriteEndElement(); // CharacterStyleRange
        w.WriteEndElement(); // ParagraphStyleRange
    }

    private static void Attr(XmlWriter w, string name, int value) =>
        w.WriteAttributeString(name, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    private static void Attr(XmlWriter w, string name, double value) => IdmlFormat.Attr(w, name, value);
    private static void Attr(XmlWriter w, string name, string value) => IdmlFormat.Attr(w, name, value);
    private static void Attr(XmlWriter w, string name, bool value) => IdmlFormat.Attr(w, name, value);
}
