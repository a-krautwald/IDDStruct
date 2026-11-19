using System.Text;
using IDDStruct.Core.Import;
using IDDStruct.Core.Model;

namespace IDDStruct.Tests;

public class CsvTests
{
    [Fact]
    public void ParsesQuotedFieldsWithDelimitersAndNewlines()
    {
        var rows = CsvParser.Parse("a,b\n\"x, y\",\"line1\nline2\"\n\"say \"\"hi\"\"\",z\r\n", ',');
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "x, y", "line1\nline2" }, rows[1]);
        Assert.Equal(new[] { "say \"hi\"", "z" }, rows[2]);
    }

    [Theory]
    [InlineData("a;b;c\n1;2;3", ';')]
    [InlineData("a,b,c\n1,2,3", ',')]
    [InlineData("a\tb\n1\t2", '\t')]
    [InlineData("\"a;b\",c,d", ',')]
    public void DetectsDelimiter(string text, char expected) =>
        Assert.Equal(expected, CsvParser.DetectDelimiter(text));

    [Fact]
    public void FallsBackToLatin1ForLegacyFiles()
    {
        var bytes = Encoding.Latin1.GetBytes("Navn;By\nSøren;Århus\n");
        var result = new CsvImporter().Import(new MemoryStream(bytes), "people.csv", new ImportOptions());
        Assert.Contains(result.Document.Blocks, b => b.Text == "Søren");
        Assert.Contains(result.Warnings, w => w.Contains("Latin-1"));
    }

    [Fact]
    public void BlockModeMapsTypesLevelsAndStyles()
    {
        const string csv = "type,level,text,style\nh1,,Title,\nparagraph,,Body **text**,\nbullet,2,Nested,\nnumbered,,Step,\nintro,,Custom styled,\npagebreak,,,\np,,Styled para,Lead\n";
        var doc = Import(csv);
        Assert.Collection(doc.Blocks,
            b => { Assert.Equal(BlockKind.Heading, b.Kind); Assert.Equal(1, b.Level); },
            b => Assert.Equal(BlockKind.Paragraph, b.Kind),
            b => { Assert.Equal(BlockKind.BulletItem, b.Kind); Assert.Equal(2, b.Level); },
            b => Assert.Equal(BlockKind.NumberedItem, b.Kind),
            b => { Assert.Equal(BlockKind.Paragraph, b.Kind); Assert.Equal("intro", b.Style); },
            b => Assert.Equal(BlockKind.PageBreak, b.Kind),
            b => Assert.Equal("Lead", b.Style));
    }

    [Fact]
    public void RecordModeGroupsAndLabels()
    {
        const string csv = "Category,Term,Definition\nA,Alpha,First\nA,Beta,Second\nB,Gamma,\"Para one\n\nPara two\"\n";
        var doc = Import(csv, new ImportOptions { HeadingColumn = "Term", GroupByColumn = "Category" });
        var outline = doc.Blocks.Select(b => $"{b.Kind}{(b.Kind == BlockKind.Heading ? b.Level : "")}:{b.Text}").ToList();
        Assert.Equal(new[]
        {
            "Heading1:A", "Heading2:Alpha", "Paragraph:**Definition:** First",
            "Heading2:Beta", "Paragraph:**Definition:** Second",
            "Heading1:B", "Heading2:Gamma", "Paragraph:**Definition:** Para one", "Paragraph:Para two",
        }, outline);
    }

    [Fact]
    public void RecordModeWithoutLabelsOrExcludedColumns()
    {
        var doc = Import("Name,Secret,Bio\nAnn,x,Writer\n", new ImportOptions { IncludeFieldLabels = false, ExcludedColumns = { "secret" } });
        Assert.Equal(new[] { "Ann", "Writer" }, doc.Blocks.Select(b => b.Text));
    }

    [Fact]
    public void WarnsOnUnknownHeadingColumn()
    {
        var result = new CsvImporter().Import(Stream("Name,Bio\nAnn,Writer\n"), "x.csv", new ImportOptions { HeadingColumn = "Nope" });
        Assert.Contains(result.Warnings, w => w.Contains("Nope"));
        Assert.Equal("Ann", result.Document.Blocks[0].Text);
    }

    private static StructuredDocument Import(string csv, ImportOptions? options = null) =>
        new CsvImporter().Import(Stream(csv), "test.csv", options ?? new ImportOptions()).Document;

    private static Stream Stream(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));
}
