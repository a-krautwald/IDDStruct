using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using IDDStruct.Core.Model;
using IDDStruct.Idml;

namespace IDDStruct.Tests;

public class IdmlExporterTests
{
    private static readonly XNamespace Pkg = "http://ns.adobe.com/AdobeInDesign/idml/1.0/packaging";

    private static StructuredDocument SampleDocument(int paragraphs = 3)
    {
        var doc = new StructuredDocument { Title = "Test" };
        doc.Blocks.Add(Block.Heading(1, "Title"));
        for (int i = 0; i < paragraphs; i++)
            doc.Blocks.Add(Block.Paragraph($"Paragraph {i} with **bold** & <special> chars.\nSecond line."));
        doc.Blocks.Add(Block.Numbered("First"));
        doc.Blocks.Add(Block.Numbered("Second"));
        doc.Blocks.Add(Block.PageBreak());
        doc.Blocks.Add(Block.Bullet("Point"));
        doc.Blocks.Add(Block.Numbered("Restart"));
        doc.Blocks.Add(Block.Paragraph("Custom", "My: Style"));
        doc.Blocks.Add(Block.FromTable(TableData.FromDelimitedText("H1|H2\na|b\nc")));
        return doc;
    }

    private static (byte[] Bytes, Dictionary<string, XDocument> Parts, Core.Export.ExportReport Report) Export(StructuredDocument doc, IdmlExportOptions? options = null)
    {
        using var ms = new MemoryStream();
        var report = new IdmlExporter(options).Export(doc, ms);
        var bytes = ms.ToArray();
        using var zip = new ZipArchive(new MemoryStream(bytes));
        var parts = zip.Entries.Where(e => e.FullName.EndsWith(".xml"))
            .ToDictionary(e => e.FullName, e => { using var s = e.Open(); return XDocument.Load(s); });
        return (bytes, parts, report);
    }

    [Fact]
    public void MimetypeIsFirstAndStored()
    {
        var (bytes, _, _) = Export(SampleDocument());
        Assert.Equal(0x04034b50, BitConverter.ToInt32(bytes, 0));
        Assert.Equal(0, BitConverter.ToInt16(bytes, 6) & 0x08);  // no data descriptor
        Assert.Equal(0, BitConverter.ToInt16(bytes, 8));          // stored
        Assert.Equal("mimetype", System.Text.Encoding.ASCII.GetString(bytes, 30, 8));
        Assert.Equal("application/vnd.adobe.indesign-idml-package", System.Text.Encoding.ASCII.GetString(bytes, 38, 43));
    }

    [Fact]
    public void DesignMapReferencesEveryPart()
    {
        var (_, parts, _) = Export(SampleDocument());
        var refs = parts["designmap.xml"].Root!.Elements().Where(e => e.Name.Namespace == Pkg).Select(e => (string)e.Attribute("src")!).ToList();
        Assert.All(refs, r => Assert.Contains(r, parts.Keys));
        Assert.Contains("Resources/Styles.xml", refs);
        Assert.Contains(refs, r => r.StartsWith("Stories/"));
    }

    [Fact]
    public void AllAppliedStylesAreDefined()
    {
        var (_, parts, report) = Export(SampleDocument());
        var defined = parts["Resources/Styles.xml"].Descendants()
            .Where(e => e.Name.LocalName is "ParagraphStyle" or "CharacterStyle")
            .Select(e => (string)e.Attribute("Self")!).ToHashSet();
        var story = parts.First(p => p.Key.StartsWith("Stories/")).Value;
        var applied = story.Descendants().SelectMany(e => e.Attributes())
            .Where(a => a.Name.LocalName is "AppliedParagraphStyle" or "AppliedCharacterStyle")
            .Select(a => a.Value).ToHashSet();
        Assert.Subset(defined, applied);
        Assert.Contains("ParagraphStyle/My%3a Style", applied);
        Assert.Contains(report.Warnings, w => w.Contains("My: Style"));
    }

    [Fact]
    public void StoryContentMatchesBlocks()
    {
        var (_, parts, _) = Export(SampleDocument(1));
        var story = parts.First(p => p.Key.StartsWith("Stories/")).Value;
        var ranges = story.Root!.Element("Story")!.Elements("ParagraphStyleRange").ToList();

        Assert.Equal("ParagraphStyle/Heading 1", (string)ranges[0].Attribute("AppliedParagraphStyle")!);
        var bodyText = string.Concat(ranges[1].Descendants("Content").Select(c => c.Value));
        Assert.Equal("Paragraph 0 with bold & <special> chars.\u2028Second line.", bodyText);
        Assert.Contains(ranges[1].Elements("CharacterStyleRange"), c => (string)c.Attribute("AppliedCharacterStyle")! == "CharacterStyle/Bold");

        // Numbering restarts at the first item of each list (here, after a top-level bullet).
        Assert.Equal("false", (string?)ranges[2].Attribute("NumberingContinue"));
        Assert.Null(ranges[3].Attribute("NumberingContinue"));
        Assert.Equal("NextPage", (string?)ranges[4].Attribute("StartParagraph"));
        Assert.Equal("false", (string?)ranges[5].Attribute("NumberingContinue"));

        // Every paragraph but the last ends in a break.
        Assert.All(ranges.SkipLast(1), r => Assert.Single(r.Descendants("Br")));
        Assert.Empty(ranges[^1].Descendants("Br"));

        var table = story.Descendants("Table").Single();
        Assert.Equal("1", (string)table.Attribute("HeaderRowCount")!);
        Assert.Equal("2", (string)table.Attribute("BodyRowCount")!);
        Assert.Equal(6, table.Elements("Cell").Count());
        Assert.Equal("1:2", (string)table.Elements("Cell").Last().Attribute("Name")!);
    }

    [Fact]
    public void TextFramesAreThreadedAcrossPages()
    {
        var (_, parts, report) = Export(SampleDocument(), new IdmlExportOptions { PageCount = 4 });
        Assert.Equal(4, report.PageCount);
        var frames = parts.Where(p => p.Key.StartsWith("Spreads/"))
            .Select(p => p.Value.Descendants("TextFrame").Single())
            .OrderBy(f => (string)f.Attribute("PreviousTextFrame")! == "n" ? 0 : 1)
            .ToList();
        var byId = frames.ToDictionary(f => (string)f.Attribute("Self")!);
        var current = frames.Single(f => (string)f.Attribute("PreviousTextFrame")! == "n");
        int count = 1;
        while ((string)current.Attribute("NextTextFrame")! != "n")
        {
            var next = byId[(string)current.Attribute("NextTextFrame")!];
            Assert.Equal((string)current.Attribute("Self")!, (string)next.Attribute("PreviousTextFrame")!);
            current = next;
            count++;
        }
        Assert.Equal(4, count);
        Assert.Equal("4", (string)parts["Resources/Preferences.xml"].Descendants("DocumentPreference").Single().Attribute("PagesPerDocument")!);
    }

    [Fact]
    public void NumbersAreCultureInvariant()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("da-DK");
            var (_, parts, _) = Export(SampleDocument());
            var height = (string)parts["Resources/Preferences.xml"].Descendants("DocumentPreference").Single().Attribute("PageHeight")!;
            Assert.Equal("841.8898", height);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void EstimatesMorePagesForLongerText()
    {
        Assert.Equal(2, PageEstimator.Estimate(SampleDocument(1))); // contains one page break
        Assert.True(PageEstimator.Estimate(SampleDocument(400)) > 5);
    }

    [Fact]
    public void NumberingContinuesAfterNestedBullet()
    {
        var doc = new StructuredDocument();
        doc.Blocks.Add(Block.Numbered("One"));
        doc.Blocks.Add(Block.Bullet("Detail", 2));
        doc.Blocks.Add(Block.Numbered("Two"));
        var (_, parts, _) = Export(doc);
        var ranges = parts.First(p => p.Key.StartsWith("Stories/")).Value.Descendants("ParagraphStyleRange").ToList();
        Assert.Equal("false", (string?)ranges[0].Attribute("NumberingContinue"));
        Assert.Null(ranges[2].Attribute("NumberingContinue"));
    }

    [Fact]
    public void ExportsEmptyDocument()
    {
        var (_, parts, report) = Export(new StructuredDocument());
        Assert.Equal(1, report.PageCount);
        Assert.Single(parts.First(p => p.Key.StartsWith("Stories/")).Value.Descendants("ParagraphStyleRange"));
    }

    [Fact]
    public void DoesNotMutateSourceStyleSheet()
    {
        var doc = SampleDocument();
        int before = doc.Styles.ParagraphStyles.Count;
        Export(doc);
        Assert.Equal(before, doc.Styles.ParagraphStyles.Count);
    }
}
