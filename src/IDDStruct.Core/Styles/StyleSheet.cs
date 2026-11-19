using IDDStruct.Core.Model;

namespace IDDStruct.Core.Styles;

public enum ListType { None, Bullet, Numbered }

/// <summary>A paragraph style to be created in the InDesign document.</summary>
public sealed class ParagraphStyleDef
{
    public string Name { get; set; } = "";
    public string? FontFamily { get; set; }
    public string? FontStyle { get; set; }
    public double PointSize { get; set; } = 10.5;
    /// <summary>Leading in points; null means InDesign "Auto".</summary>
    public double? Leading { get; set; }
    public double SpaceBefore { get; set; }
    public double SpaceAfter { get; set; }
    public double LeftIndent { get; set; }
    public double FirstLineIndent { get; set; }
    /// <summary>Number of lines to keep with the next paragraph (headings).</summary>
    public int KeepWithNext { get; set; }
    public ListType List { get; set; } = ListType.None;
    public int ListLevel { get; set; } = 1;
}

/// <summary>A character style applied to inline formatting.</summary>
public sealed class CharacterStyleDef
{
    public string Name { get; set; } = "";
    public string? FontStyle { get; set; }
}

/// <summary>
/// The set of styles a document uses, and the rules that map blocks onto paragraph styles.
/// Style names are what appear in InDesign's Paragraph Styles panel.
/// </summary>
public sealed class StyleSheet
{
    public const string BodyStyle = "Body";
    public const string TableBodyStyle = "Table Body";
    public const string TableHeaderStyle = "Table Header";
    public const string BoldStyle = "Bold";
    public const string ItalicStyle = "Italic";
    public const string BoldItalicStyle = "Bold Italic";

    public List<ParagraphStyleDef> ParagraphStyles { get; set; } = new();
    public List<CharacterStyleDef> CharacterStyles { get; set; } = new();

    public static string HeadingStyle(int level) => $"Heading {Math.Clamp(level, 1, 6)}";
    public static string BulletStyle(int level) => $"Bullet {Math.Clamp(level, 1, 3)}";
    public static string NumberedStyle(int level) => $"Numbered {Math.Clamp(level, 1, 3)}";

    public string ResolveParagraphStyle(Block block)
    {
        if (!string.IsNullOrWhiteSpace(block.Style)) return block.Style.Trim();
        return block.Kind switch
        {
            BlockKind.Heading => HeadingStyle(block.Level),
            BlockKind.BulletItem => BulletStyle(block.Level),
            BlockKind.NumberedItem => NumberedStyle(block.Level),
            _ => BodyStyle,
        };
    }

    public static string ResolveCharacterStyle(TextRun run) =>
        run.Bold && run.Italic ? BoldItalicStyle : run.Bold ? BoldStyle : run.Italic ? ItalicStyle : "";

    public ParagraphStyleDef? FindParagraphStyle(string name) =>
        ParagraphStyles.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// Returns the definition for <paramref name="name"/>, creating one based on Body if
    /// the document references a style the sheet does not define.
    /// </summary>
    public ParagraphStyleDef GetOrAddParagraphStyle(string name)
    {
        var existing = FindParagraphStyle(name);
        if (existing is not null) return existing;
        var body = FindParagraphStyle(BodyStyle);
        var created = new ParagraphStyleDef
        {
            Name = name,
            FontFamily = body?.FontFamily,
            PointSize = body?.PointSize ?? 10.5,
            Leading = body?.Leading,
            SpaceAfter = body?.SpaceAfter ?? 0,
        };
        ParagraphStyles.Add(created);
        return created;
    }

    public static StyleSheet CreateDefault(string bodyFont = "Minion Pro", string headingFont = "Myriad Pro")
    {
        var sheet = new StyleSheet();
        double[] headingSizes = { 24, 18, 14, 12, 11, 10.5 };
        for (int level = 1; level <= 6; level++)
        {
            sheet.ParagraphStyles.Add(new ParagraphStyleDef
            {
                Name = HeadingStyle(level),
                FontFamily = headingFont,
                FontStyle = level <= 4 ? "Bold" : "Semibold",
                PointSize = headingSizes[level - 1],
                Leading = Math.Round(headingSizes[level - 1] * 1.2, 1),
                SpaceBefore = level == 1 ? 0 : headingSizes[level - 1] * 0.9,
                SpaceAfter = headingSizes[level - 1] * 0.4,
                KeepWithNext = 2,
            });
        }

        sheet.ParagraphStyles.Add(new ParagraphStyleDef
        {
            Name = BodyStyle, FontFamily = bodyFont, FontStyle = "Regular",
            PointSize = 10.5, Leading = 14, SpaceAfter = 6,
        });
        sheet.ParagraphStyles.Add(new ParagraphStyleDef
        {
            Name = "Quote", FontFamily = bodyFont, FontStyle = "Italic",
            PointSize = 10.5, Leading = 14, SpaceBefore = 3, SpaceAfter = 9, LeftIndent = 18,
        });

        for (int level = 1; level <= 3; level++)
        {
            foreach (var list in new[] { ListType.Bullet, ListType.Numbered })
            {
                sheet.ParagraphStyles.Add(new ParagraphStyleDef
                {
                    Name = list == ListType.Bullet ? BulletStyle(level) : NumberedStyle(level),
                    FontFamily = bodyFont, FontStyle = "Regular",
                    PointSize = 10.5, Leading = 14, SpaceAfter = 3,
                    LeftIndent = 14 * level, FirstLineIndent = -14,
                    List = list, ListLevel = level,
                });
            }
        }

        sheet.ParagraphStyles.Add(new ParagraphStyleDef
        {
            Name = TableBodyStyle, FontFamily = bodyFont, FontStyle = "Regular", PointSize = 9, Leading = 12,
        });
        sheet.ParagraphStyles.Add(new ParagraphStyleDef
        {
            Name = TableHeaderStyle, FontFamily = headingFont, FontStyle = "Bold", PointSize = 9, Leading = 12,
        });

        sheet.CharacterStyles.Add(new CharacterStyleDef { Name = BoldStyle, FontStyle = "Bold" });
        sheet.CharacterStyles.Add(new CharacterStyleDef { Name = ItalicStyle, FontStyle = "Italic" });
        sheet.CharacterStyles.Add(new CharacterStyleDef { Name = BoldItalicStyle, FontStyle = "Bold Italic" });
        return sheet;
    }
}
