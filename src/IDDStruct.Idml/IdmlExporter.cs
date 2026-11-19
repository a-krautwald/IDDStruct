using System.IO.Compression;
using System.Xml;
using IDDStruct.Core.Export;
using IDDStruct.Core.Model;
using IDDStruct.Core.Styles;
using static IDDStruct.Idml.IdmlFormat;

namespace IDDStruct.Idml;

public sealed class IdmlExportOptions
{
    /// <summary>Fixed number of pages to create; estimated from content when null.</summary>
    public int? PageCount { get; set; }

    /// <summary>Multiplier applied to the estimated page count to avoid overset text.</summary>
    public double PageEstimateSafetyFactor { get; set; } = 1.15;
}

/// <summary>
/// Writes an InDesign Markup Language (IDML) package: one story containing all content,
/// flowed through threaded, margin-sized text frames on single-page spreads, with paragraph
/// and character styles created from the document's <see cref="StyleSheet"/>.
/// </summary>
public sealed class IdmlExporter : IDocumentExporter
{
    private readonly IdmlExportOptions _options;

    public IdmlExporter(IdmlExportOptions? options = null) => _options = options ?? new IdmlExportOptions();

    public string DisplayName => "InDesign Markup (IDML)";
    public string FileExtension => ".idml";

    public ExportReport Export(StructuredDocument document, Stream output)
    {
        // Work on a copy of the style sheet so styles added for unknown names don't leak into the source.
        var doc = new StructuredDocument
        {
            Title = document.Title,
            Page = document.Page,
            Blocks = document.Blocks,
            Styles = new StyleSheet
            {
                ParagraphStyles = document.Styles.ParagraphStyles.ToList(),
                CharacterStyles = document.Styles.CharacterStyles.ToList(),
            },
        };

        var warnings = new List<string>();
        foreach (var name in doc.UsedParagraphStyles())
        {
            if (doc.Styles.FindParagraphStyle(name) is null)
            {
                doc.Styles.GetOrAddParagraphStyle(name);
                warnings.Add($"Paragraph style '{name}' is not defined; created it based on Body.");
            }
        }
        foreach (var name in new[] { StyleSheet.BoldStyle, StyleSheet.ItalicStyle, StyleSheet.BoldItalicStyle })
        {
            if (!doc.Styles.CharacterStyles.Any(s => s.Name == name))
                doc.Styles.CharacterStyles.Add(new CharacterStyleDef { Name = name, FontStyle = name });
        }

        int pageCount = Math.Max(1, _options.PageCount ?? PageEstimator.Estimate(doc, _options.PageEstimateSafetyFactor));
        var ids = new IdGenerator();
        string storyId = ids.Next();
        string layerId = ids.Next();
        string backingStoryId = ids.Next();

        var page = doc.Page;
        int columns = Math.Max(1, page.Columns);
        double columnWidth = (page.TextAreaWidth - page.ColumnGutter * (columns - 1)) / columns;

        var spreadIds = Enumerable.Range(0, pageCount).Select(_ => ids.Next()).ToList();
        var frameIds = Enumerable.Range(0, pageCount).Select(_ => ids.Next()).ToList();

        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            // The mimetype entry must come first and be stored uncompressed.
            var mime = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var s = mime.Open())
            {
                var bytes = System.Text.Encoding.ASCII.GetBytes(MimeType);
                s.Write(bytes, 0, bytes.Length);
            }

            Part(zip, "META-INF/container.xml", WriteContainer);
            Part(zip, "designmap.xml", w => WriteDesignMap(w, doc, storyId, layerId, spreadIds));
            Part(zip, "Resources/Graphic.xml", WriteGraphic);
            Part(zip, "Resources/Fonts.xml", w => { StartPart(w, "Fonts"); EndPart(w); });
            Part(zip, "Resources/Styles.xml", w => WriteStyles(w, doc.Styles));
            Part(zip, "Resources/Preferences.xml", w => WritePreferences(w, page, pageCount));
            Part(zip, "XML/Tags.xml", WriteTags);
            Part(zip, "XML/BackingStory.xml", w => WriteBackingStory(w, backingStoryId));
            for (int i = 0; i < pageCount; i++)
            {
                int index = i;
                Part(zip, $"Spreads/Spread_{spreadIds[i]}.xml", w => WriteSpread(w, page, index, pageCount, spreadIds, frameIds, storyId, layerId, ids));
            }
            Part(zip, $"Stories/Story_{storyId}.xml", w => new StoryWriter(doc, ids, columnWidth).Write(w, storyId));
        }

        var report = new ExportReport { PageCount = pageCount };
        report.Warnings.AddRange(warnings);
        return report;
    }

    private static void Part(ZipArchive zip, string path, Action<XmlWriter> write)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var w = CreateWriter(stream);
        write(w);
    }

    private static void WriteContainer(XmlWriter w)
    {
        const string ns = "urn:oasis:names:tc:opendocument:xmlns:container";
        w.WriteStartDocument(standalone: true);
        w.WriteStartElement("container", ns);
        w.WriteAttributeString("version", "1.0");
        w.WriteStartElement("rootfiles", ns);
        w.WriteStartElement("rootfile", ns);
        w.WriteAttributeString("full-path", "designmap.xml");
        w.WriteAttributeString("media-type", "text/xml");
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndDocument();
    }

    private static void WriteDesignMap(XmlWriter w, StructuredDocument doc, string storyId, string layerId, List<string> spreadIds)
    {
        w.WriteStartDocument(standalone: true);
        w.WriteProcessingInstruction("aid", "style=\"50\" type=\"document\" readerVersion=\"6.0\" featureSet=\"257\" product=\"8.0(370)\"");
        w.WriteStartElement("Document");
        w.WriteAttributeString("xmlns", "idPkg", null, PackagingNs);
        Attr(w, "DOMVersion", DomVersion);
        Attr(w, "Self", "d");
        Attr(w, "StoryList", storyId);
        Attr(w, "ZeroPoint", "0 0");
        Attr(w, "ActiveLayer", layerId);

        w.WriteStartElement("Properties");
        w.WriteStartElement("Label");
        w.WriteStartElement("KeyValuePair");
        Attr(w, "Key", "IDDStruct.Title");
        Attr(w, "Value", doc.Title);
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();

        PkgRef(w, "Graphic", "Resources/Graphic.xml");
        PkgRef(w, "Fonts", "Resources/Fonts.xml");
        PkgRef(w, "Styles", "Resources/Styles.xml");
        PkgRef(w, "Preferences", "Resources/Preferences.xml");
        PkgRef(w, "Tags", "XML/Tags.xml");

        w.WriteStartElement("Layer");
        Attr(w, "Self", layerId);
        Attr(w, "Name", "Layer 1");
        Attr(w, "Visible", true);
        Attr(w, "Locked", false);
        Attr(w, "IgnoreWrap", false);
        Attr(w, "ShowGuides", true);
        Attr(w, "LockGuides", false);
        Attr(w, "UI", true);
        Attr(w, "Expendable", true);
        Attr(w, "Printable", true);
        w.WriteStartElement("Properties");
        w.WriteStartElement("LayerColor");
        Attr(w, "type", "enumeration");
        w.WriteString("LightBlue");
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();

        foreach (var id in spreadIds) PkgRef(w, "Spread", $"Spreads/Spread_{id}.xml");
        PkgRef(w, "BackingStory", "XML/BackingStory.xml");
        PkgRef(w, "Story", $"Stories/Story_{storyId}.xml");

        w.WriteEndElement();
        w.WriteEndDocument();
    }

    private static void PkgRef(XmlWriter w, string part, string src)
    {
        w.WriteStartElement("idPkg", part, PackagingNs);
        w.WriteAttributeString("src", src);
        w.WriteEndElement();
    }

    private static void WriteGraphic(XmlWriter w)
    {
        StartPart(w, "Graphic");
        void Color(string name, string model, string value, string over)
        {
            w.WriteStartElement("Color");
            Attr(w, "Self", $"Color/{name}");
            Attr(w, "Model", model);
            Attr(w, "Space", "CMYK");
            Attr(w, "ColorValue", value);
            Attr(w, "ColorOverride", over);
            Attr(w, "AlternateSpace", "NoAlternateColor");
            Attr(w, "AlternateColorValue", "");
            Attr(w, "Name", name);
            Attr(w, "ColorEditable", false);
            Attr(w, "ColorRemovable", false);
            Attr(w, "Visible", true);
            Attr(w, "SwatchCreatorID", "7937");
            w.WriteEndElement();
        }
        Color("Black", "Process", "0 0 0 100", "Specialblack");
        Color("Paper", "Process", "0 0 0 0", "Specialpaper");
        Color("Registration", "Registration", "100 100 100 100", "Specialregistration");

        w.WriteStartElement("Swatch");
        Attr(w, "Self", "Swatch/None");
        Attr(w, "Name", "None");
        Attr(w, "ColorEditable", false);
        Attr(w, "ColorRemovable", false);
        Attr(w, "Visible", true);
        Attr(w, "SwatchCreatorID", "7937");
        w.WriteEndElement();

        w.WriteStartElement("StrokeStyle");
        Attr(w, "Self", "StrokeStyle/$ID/Solid");
        Attr(w, "Name", "$ID/Solid");
        w.WriteEndElement();
        EndPart(w);
    }

    private static void WriteStyles(XmlWriter w, StyleSheet styles)
    {
        StartPart(w, "Styles");

        w.WriteStartElement("RootCharacterStyleGroup");
        Attr(w, "Self", "u79");
        w.WriteStartElement("CharacterStyle");
        Attr(w, "Self", NoCharacterStyle);
        Attr(w, "Imported", false);
        Attr(w, "Name", "$ID/[No character style]");
        w.WriteEndElement();
        foreach (var cs in styles.CharacterStyles)
        {
            w.WriteStartElement("CharacterStyle");
            Attr(w, "Self", CharacterStyleRef(cs.Name));
            Attr(w, "Imported", false);
            Attr(w, "Name", cs.Name);
            if (cs.FontStyle is not null) Attr(w, "FontStyle", cs.FontStyle);
            w.WriteStartElement("Properties");
            BasedOn(w, "$ID/[No character style]", isObject: false);
            w.WriteEndElement();
            w.WriteEndElement();
        }
        w.WriteEndElement();

        w.WriteStartElement("RootParagraphStyleGroup");
        Attr(w, "Self", "u78");
        w.WriteStartElement("ParagraphStyle");
        Attr(w, "Self", NoParagraphStyle);
        Attr(w, "Name", "$ID/[No paragraph style]");
        Attr(w, "Imported", false);
        w.WriteEndElement();
        w.WriteStartElement("ParagraphStyle");
        Attr(w, "Self", NormalParagraphStyle);
        Attr(w, "Name", "$ID/NormalParagraphStyle");
        Attr(w, "Imported", false);
        w.WriteStartElement("Properties");
        BasedOn(w, "$ID/[No paragraph style]", isObject: false);
        w.WriteEndElement();
        w.WriteEndElement();
        foreach (var ps in styles.ParagraphStyles) WriteParagraphStyle(w, ps);
        w.WriteEndElement();

        w.WriteStartElement("RootCellStyleGroup");
        Attr(w, "Self", "u8a");
        w.WriteStartElement("CellStyle");
        Attr(w, "Self", "CellStyle/$ID/[None]");
        Attr(w, "Name", "$ID/[None]");
        w.WriteEndElement();
        w.WriteEndElement();

        w.WriteStartElement("RootTableStyleGroup");
        Attr(w, "Self", "u8c");
        w.WriteStartElement("TableStyle");
        Attr(w, "Self", "TableStyle/$ID/[No table style]");
        Attr(w, "Name", "$ID/[No table style]");
        w.WriteEndElement();
        w.WriteStartElement("TableStyle");
        Attr(w, "Self", "TableStyle/$ID/[Basic Table]");
        Attr(w, "Name", "$ID/[Basic Table]");
        w.WriteStartElement("Properties");
        BasedOn(w, "$ID/[No table style]", isObject: false);
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();

        w.WriteStartElement("RootObjectStyleGroup");
        Attr(w, "Self", "u97");
        foreach (var name in new[] { "$ID/[None]", "$ID/[Normal Graphics Frame]", "$ID/[Normal Text Frame]", "$ID/[Normal Grid]" })
        {
            w.WriteStartElement("ObjectStyle");
            Attr(w, "Self", "ObjectStyle/" + name);
            Attr(w, "Name", name);
            w.WriteEndElement();
        }
        w.WriteEndElement();

        EndPart(w);
    }

    private static void WriteParagraphStyle(XmlWriter w, ParagraphStyleDef ps)
    {
        w.WriteStartElement("ParagraphStyle");
        Attr(w, "Self", ParagraphStyleRef(ps.Name));
        Attr(w, "Name", ps.Name);
        Attr(w, "Imported", false);
        Attr(w, "NextStyle", ParagraphStyleRef(ps.Name));
        if (ps.FontStyle is not null) Attr(w, "FontStyle", ps.FontStyle);
        Attr(w, "PointSize", ps.PointSize);
        Attr(w, "SpaceBefore", ps.SpaceBefore);
        Attr(w, "SpaceAfter", ps.SpaceAfter);
        Attr(w, "LeftIndent", ps.LeftIndent);
        Attr(w, "FirstLineIndent", ps.FirstLineIndent);
        if (ps.KeepWithNext > 0) Attr(w, "KeepWithNext", ps.KeepWithNext);
        switch (ps.List)
        {
            case ListType.Bullet:
                Attr(w, "BulletsAndNumberingListType", "BulletList");
                Attr(w, "BulletsTextAfter", "^t");
                break;
            case ListType.Numbered:
                Attr(w, "BulletsAndNumberingListType", "NumberedList");
                Attr(w, "NumberingLevel", ps.ListLevel);
                Attr(w, "NumberingFormat", ps.ListLevel switch { 2 => "a, b, c, d...", 3 => "i, ii, iii, iv...", _ => "1, 2, 3, 4..." });
                Attr(w, "NumberingExpression", $"^{ps.ListLevel}.^t");
                Attr(w, "NumberingContinue", true);
                break;
            default:
                Attr(w, "BulletsAndNumberingListType", "NoList");
                break;
        }

        w.WriteStartElement("Properties");
        BasedOn(w, NormalParagraphStyle, isObject: true);
        if (ps.Leading is { } leading)
        {
            w.WriteStartElement("Leading");
            Attr(w, "type", "unit");
            w.WriteString(Num(leading));
            w.WriteEndElement();
        }
        if (ps.FontFamily is not null)
        {
            w.WriteStartElement("AppliedFont");
            Attr(w, "type", "string");
            w.WriteString(ps.FontFamily);
            w.WriteEndElement();
        }
        if (ps.List == ListType.Bullet)
        {
            w.WriteStartElement("BulletChar");
            Attr(w, "BulletCharacterType", "UnicodeOnly");
            Attr(w, "BulletCharacterValue", ps.ListLevel switch { 2 => 8211, 3 => 9702, _ => 8226 }); // • – ◦
            w.WriteEndElement();
        }
        w.WriteEndElement();
        w.WriteEndElement();
    }

    private static void BasedOn(XmlWriter w, string value, bool isObject)
    {
        w.WriteStartElement("BasedOn");
        Attr(w, "type", isObject ? "object" : "string");
        w.WriteString(value);
        w.WriteEndElement();
    }

    private static void WritePreferences(XmlWriter w, PageSetup page, int pageCount)
    {
        StartPart(w, "Preferences");
        w.WriteStartElement("DocumentPreference");
        Attr(w, "PageHeight", page.Height);
        Attr(w, "PageWidth", page.Width);
        Attr(w, "PagesPerDocument", pageCount);
        Attr(w, "FacingPages", false);
        Attr(w, "DocumentBleedTopOffset", 0);
        Attr(w, "DocumentBleedBottomOffset", 0);
        Attr(w, "DocumentBleedInsideOrLeftOffset", 0);
        Attr(w, "DocumentBleedOutsideOrRightOffset", 0);
        Attr(w, "PageBinding", "LeftToRight");
        Attr(w, "AllowPageShuffle", true);
        Attr(w, "OverprintBlack", true);
        Attr(w, "ColumnGuideLocked", true);
        Attr(w, "MasterTextFrame", false);
        Attr(w, "SnippetImportUsesOriginalLocation", false);
        w.WriteEndElement();

        WriteMarginPreference(w, page);

        w.WriteStartElement("TextPreference");
        Attr(w, "SmartTextReflow", true);
        Attr(w, "AddPages", "EndOfStory");
        Attr(w, "LimitToMasterTextFrames", false);
        Attr(w, "PreserveFacingPageSpreads", false);
        Attr(w, "DeleteEmptyPages", true);
        Attr(w, "TypographersQuotes", true);
        w.WriteEndElement();
        EndPart(w);
    }

    private static void WriteMarginPreference(XmlWriter w, PageSetup page)
    {
        int columns = Math.Max(1, page.Columns);
        double columnWidth = (page.TextAreaWidth - page.ColumnGutter * (columns - 1)) / columns;
        var positions = new List<string>();
        for (int c = 0; c < columns; c++)
        {
            double start = c * (columnWidth + page.ColumnGutter);
            positions.Add(Num(start));
            positions.Add(Num(start + columnWidth));
        }

        w.WriteStartElement("MarginPreference");
        Attr(w, "ColumnCount", columns);
        Attr(w, "ColumnGutter", page.ColumnGutter);
        Attr(w, "Top", page.MarginTop);
        Attr(w, "Bottom", page.MarginBottom);
        Attr(w, "Left", page.MarginInside);
        Attr(w, "Right", page.MarginOutside);
        Attr(w, "ColumnDirection", "Horizontal");
        Attr(w, "ColumnsPositions", string.Join(" ", positions));
        w.WriteEndElement();
    }

    private static void WriteTags(XmlWriter w)
    {
        StartPart(w, "Tags");
        w.WriteStartElement("XMLTag");
        Attr(w, "Self", "XMLTag/Root");
        Attr(w, "Name", "Root");
        w.WriteStartElement("Properties");
        w.WriteStartElement("TagColor");
        Attr(w, "type", "enumeration");
        w.WriteString("LightBlue");
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
        EndPart(w);
    }

    private static void WriteBackingStory(XmlWriter w, string id)
    {
        StartPart(w, "BackingStory");
        w.WriteStartElement("XmlStory");
        Attr(w, "Self", id);
        Attr(w, "AppliedTOCStyle", "n");
        Attr(w, "TrackChanges", false);
        Attr(w, "StoryTitle", "$ID/");
        Attr(w, "AppliedNamedGrid", "n");
        w.WriteStartElement("ParagraphStyleRange");
        Attr(w, "AppliedParagraphStyle", NormalParagraphStyle);
        w.WriteStartElement("CharacterStyleRange");
        Attr(w, "AppliedCharacterStyle", NoCharacterStyle);
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
        EndPart(w);
    }

    private static void WriteSpread(XmlWriter w, PageSetup page, int index, int pageCount,
        List<string> spreadIds, List<string> frameIds, string storyId, string layerId, IdGenerator ids)
    {
        const double spreadGap = 36;
        double h = page.Height, wd = page.Width;

        StartPart(w, "Spread");
        w.WriteStartElement("Spread");
        Attr(w, "Self", spreadIds[index]);
        Attr(w, "FlattenerOverride", "Default");
        Attr(w, "ShowMasterItems", true);
        Attr(w, "PageCount", 1);
        Attr(w, "BindingLocation", 0);
        Attr(w, "AllowPageShuffle", true);
        Attr(w, "ItemTransform", $"1 0 0 1 0 {Num(index * (h + spreadGap))}");

        // Spread coordinates: x from the binding edge, y centred on the page.
        w.WriteStartElement("Page");
        Attr(w, "Self", ids.Next());
        Attr(w, "Name", (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Attr(w, "AppliedMaster", "n");
        Attr(w, "OverrideList", "");
        Attr(w, "TabOrder", "");
        Attr(w, "GridStartingPoint", "TopOutside");
        Attr(w, "UseMasterGrid", true);
        Attr(w, "GeometricBounds", $"0 0 {Num(h)} {Num(wd)}");
        Attr(w, "ItemTransform", $"1 0 0 1 0 {Num(-h / 2)}");
        Attr(w, "MasterPageTransform", "1 0 0 1 0 0");
        WriteMarginPreference(w, page);
        w.WriteEndElement();

        double left = page.MarginInside, right = wd - page.MarginOutside;
        double top = -h / 2 + page.MarginTop, bottom = h / 2 - page.MarginBottom;

        w.WriteStartElement("TextFrame");
        Attr(w, "Self", frameIds[index]);
        Attr(w, "ParentStory", storyId);
        Attr(w, "PreviousTextFrame", index == 0 ? "n" : frameIds[index - 1]);
        Attr(w, "NextTextFrame", index == pageCount - 1 ? "n" : frameIds[index + 1]);
        Attr(w, "ContentType", "TextType");
        Attr(w, "ItemLayer", layerId);
        Attr(w, "Visible", true);
        Attr(w, "Name", "$ID/");
        Attr(w, "AppliedObjectStyle", "ObjectStyle/$ID/[Normal Text Frame]");
        Attr(w, "ItemTransform", "1 0 0 1 0 0");

        w.WriteStartElement("Properties");
        w.WriteStartElement("PathGeometry");
        w.WriteStartElement("GeometryPathType");
        Attr(w, "PathOpen", false);
        w.WriteStartElement("PathPointArray");
        foreach (var (x, y) in new[] { (left, top), (left, bottom), (right, bottom), (right, top) })
        {
            var point = $"{Num(x)} {Num(y)}";
            w.WriteStartElement("PathPointType");
            Attr(w, "Anchor", point);
            Attr(w, "LeftDirection", point);
            Attr(w, "RightDirection", point);
            w.WriteEndElement();
        }
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement(); // Properties

        w.WriteStartElement("TextFramePreference");
        Attr(w, "TextColumnCount", Math.Max(1, page.Columns));
        Attr(w, "TextColumnGutter", page.ColumnGutter);
        Attr(w, "UseFixedColumnWidth", false);
        w.WriteEndElement();

        w.WriteEndElement(); // TextFrame
        w.WriteEndElement(); // Spread
        EndPart(w);
    }

    private static void Attr(XmlWriter w, string name, int value) =>
        w.WriteAttributeString(name, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    private static void Attr(XmlWriter w, string name, double value) => IdmlFormat.Attr(w, name, value);
    private static void Attr(XmlWriter w, string name, string value) => IdmlFormat.Attr(w, name, value);
    private static void Attr(XmlWriter w, string name, bool value) => IdmlFormat.Attr(w, name, value);
}
