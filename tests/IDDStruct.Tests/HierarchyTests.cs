using System.Text;
using IDDStruct.Core.Import;
using IDDStruct.Core.Model;

namespace IDDStruct.Tests;

public class HierarchyTests
{
    private static StructuredDocument Json(string json, ImportOptions? o = null) =>
        new JsonImporter().Import(new MemoryStream(Encoding.UTF8.GetBytes(json)), "t.json", o ?? new ImportOptions()).Document;

    private static StructuredDocument Xml(string xml) =>
        new XmlImporter().Import(new MemoryStream(Encoding.UTF8.GetBytes(xml)), "t.xml", new ImportOptions()).Document;

    private static string Outline(StructuredDocument d) => string.Join("|", d.Blocks.Select(b => b.Kind switch
    {
        BlockKind.Heading => $"H{b.Level}:{b.Text}",
        BlockKind.BulletItem => $"*{b.Level}:{b.Text}",
        BlockKind.NumberedItem => $"#{b.Level}:{b.Text}",
        BlockKind.Table => $"T{b.Table!.Rows.Count}x{b.Table.ColumnCount}h{b.Table.HeaderRows}",
        BlockKind.PageBreak => "BR",
        _ => b.Style is null ? b.Text : $"{b.Text}[{b.Style}]",
    }));

    [Fact]
    public void JsonSectionsBecomeNestedHeadings()
    {
        var doc = Json("""{"title":"Doc","intro":"Hi","chapters":[{"title":"One","text":"A","tags":["x","y"],"sections":[{"title":"Sub","text":"B"}]}]}""");
        Assert.Equal("Doc", doc.Title);
        Assert.Equal("H1:Doc|Hi|H2:One|A|*1:x|*1:y|H3:Sub|B", Outline(doc));
    }

    [Fact]
    public void JsonLabelsGenericFieldsUnlessDisabled()
    {
        Assert.Equal("**First name:** Ann", Json("""{"firstName":"Ann"}""").Blocks.Single().Text);
        Assert.Equal("Ann", Json("""{"firstName":"Ann"}""", new ImportOptions { LabelScalarFields = false }).Blocks.Single().Text);
    }

    [Fact]
    public void JsonBlockListIsMappedByType()
    {
        var doc = Json("""
            {"title":"T","blocks":[
              {"type":"h2","text":"Head"},
              {"type":"numbered","text":"One"},
              {"type":"pagebreak"},
              {"type":"Lead","text":"Custom"},
              {"type":"table","headerRows":1,"rows":[["A","B"],["1","2"]]},
              {"type":"list","items":["p","q"]}
            ]}
            """);
        Assert.Equal("T", doc.Title);
        Assert.Equal("H2:Head|#1:One|BR|Custom[Lead]|T2x2h1|*1:p|*1:q", Outline(doc));
    }

    [Fact]
    public void JsonArrayOfObjectsTableUsesKeysAsHeader()
    {
        var doc = Json("""{"table":[{"name":"A","qty":1},{"name":"B","qty":2}]}""");
        var table = doc.Blocks.Single().Table!;
        Assert.Equal(new[] { "Name", "Qty" }, table.Rows[0]);
        Assert.Equal(3, table.Rows.Count);
    }

    [Fact]
    public void XmlHandlesHtmlLikeStructureAndInlineFormatting()
    {
        var doc = Xml("""
            <doc>
              <title>Manual</title>
              <chapter>
                <title>Start</title>
                <p>Use <b>bold</b> and <em>italic</em>
                   text.</p>
                <ul><li>One</li><li>Two<ol><li>Sub</li></ol></li></ul>
                <h4>Deep</h4>
                <p style="Lead">Styled</p>
                <pagebreak/>
                <table><thead><tr><th>A</th><th>B</th></tr></thead><tbody><tr><td>1</td><td>2</td></tr></tbody></table>
              </chapter>
            </doc>
            """);
        Assert.Equal("Manual", doc.Title);
        Assert.Equal("H1:Manual|H2:Start|Use **bold** and *italic* text.|*1:One|*1:Two|#2:Sub|H4:Deep|Styled[Lead]|BR|T2x2h1", Outline(doc));
    }

    [Fact]
    public void XmlLineBreakBecomesSoftReturn()
    {
        var doc = Xml("<root><p>Line one<br/>Line two</p></root>");
        Assert.Equal("Line one\nLine two", doc.Blocks.Single().Text);
    }

    [Theory]
    [InlineData("first_name", "First name")]
    [InlineData("firstName", "First name")]
    [InlineData("page-count", "Page count")]
    public void HumanisesNames(string input, string expected) =>
        Assert.Equal(expected, IDDStruct.Core.Import.Mapping.HierarchyMapper.Humanise(input));
}
