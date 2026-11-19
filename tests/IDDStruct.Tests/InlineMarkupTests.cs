using IDDStruct.Core.Model;

namespace IDDStruct.Tests;

public class InlineMarkupTests
{
    [Fact]
    public void ParsesBoldItalicAndBoth()
    {
        var runs = InlineMarkup.Parse("Plain **bold** and *italic* and ***both***.");
        Assert.Equal(new[]
        {
            new TextRun("Plain "), new TextRun("bold", Bold: true), new TextRun(" and "),
            new TextRun("italic", Italic: true), new TextRun(" and "),
            new TextRun("both", Bold: true, Italic: true), new TextRun("."),
        }, runs);
    }

    [Theory]
    [InlineData("5 * 3 = 15")]
    [InlineData("A lone * star")]
    [InlineData("Price: 10 ** 2")]
    public void LeavesSpacedAsterisksLiteral(string text) =>
        Assert.Equal(text, InlineMarkup.ToPlainText(text));

    [Fact]
    public void HonoursEscapes()
    {
        var runs = InlineMarkup.Parse(@"Not \*italic\* here");
        Assert.Single(runs);
        Assert.Equal("Not *italic* here", runs[0].Text);
    }

    [Theory]
    [InlineData("Plain **bold** *it* ***bi*** end")]
    [InlineData("Literal \\* star and \\\\ backslash")]
    public void RoundTripsThroughMarkup(string markup)
    {
        var runs = InlineMarkup.Parse(markup);
        Assert.Equal(runs, InlineMarkup.Parse(InlineMarkup.ToMarkup(runs)));
    }
}
