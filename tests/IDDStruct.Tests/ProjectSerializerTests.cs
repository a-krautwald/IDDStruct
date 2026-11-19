using IDDStruct.Core.Model;
using IDDStruct.Core.Serialization;

namespace IDDStruct.Tests;

public class ProjectSerializerTests
{
    [Fact]
    public void RoundTripsDocument()
    {
        var doc = new StructuredDocument { Title = "Round trip", Page = PageSetup.FromPreset("A5") };
        doc.Blocks.Add(Block.Heading(2, "Head"));
        doc.Blocks.Add(Block.Paragraph("Body **bold**", "Lead"));
        doc.Blocks.Add(Block.FromTable(TableData.FromDelimitedText("A|B\n1|2")));
        doc.Blocks.Add(Block.PageBreak());

        var copy = ProjectSerializer.Deserialize(ProjectSerializer.Serialize(doc));

        Assert.Equal("Round trip", copy.Title);
        Assert.Equal("A5", copy.Page.PresetName);
        Assert.Equal(doc.Blocks.Select(b => b.ToString()), copy.Blocks.Select(b => b.ToString()));
        Assert.Equal("Lead", copy.Blocks[1].Style);
        Assert.Equal(doc.Styles.ParagraphStyles.Count, copy.Styles.ParagraphStyles.Count);
    }

    [Fact]
    public void RejectsForeignJson() =>
        Assert.Throws<InvalidDataException>(() => ProjectSerializer.Deserialize("""{"format":"other","version":1}"""));
}
