using ClosedXML.Excel;
using IDDStruct.Core.Import;
using IDDStruct.Core.Model;

namespace IDDStruct.Tests;

public class XlsxTests
{
    private static MemoryStream Workbook()
    {
        using var wb = new XLWorkbook();
        var people = wb.AddWorksheet("People");
        people.Cell(1, 1).Value = "Name";
        people.Cell(1, 2).Value = "Joined";
        people.Cell(2, 1).Value = "Ann";
        people.Cell(2, 2).Value = 2021;
        var content = wb.AddWorksheet("Content");
        content.Cell(1, 1).Value = "type";
        content.Cell(1, 2).Value = "text";
        content.Cell(2, 1).Value = "h1";
        content.Cell(2, 2).Value = "Intro";
        content.Cell(3, 1).Value = "p";
        content.Cell(3, 2).Value = "Hello";
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void ImportsFirstSheetByDefault()
    {
        var doc = new XlsxImporter().Import(Workbook(), "book.xlsx", new ImportOptions()).Document;
        Assert.Equal(new[] { "Ann", "**Joined:** 2021" }, doc.Blocks.Select(b => b.Text));
    }

    [Fact]
    public void ImportsNamedSheet()
    {
        var doc = new XlsxImporter().Import(Workbook(), "book.xlsx", new ImportOptions { SheetName = "Content" }).Document;
        Assert.Equal(BlockKind.Heading, doc.Blocks[0].Kind);
        Assert.Equal("Hello", doc.Blocks[1].Text);
    }

    [Fact]
    public void AllSheetsNestsUnderSheetHeadings()
    {
        var doc = new XlsxImporter().Import(Workbook(), "book.xlsx", new ImportOptions { AllSheets = true }).Document;
        var outline = doc.Blocks.Select(b => b.Kind == BlockKind.Heading ? $"H{b.Level} {b.Text}" : b.Text).ToList();
        Assert.Equal(new[] { "H1 People", "H2 Ann", "**Joined:** 2021", "H1 Content", "H2 Intro", "Hello" }, outline);
    }

    [Fact]
    public void UnknownSheetThrows() =>
        Assert.Throws<InvalidDataException>(() =>
            new XlsxImporter().Import(Workbook(), "book.xlsx", new ImportOptions { SheetName = "Missing" }));
}
