using IDDStruct.Core.Styles;

namespace IDDStruct.Core.Model;

/// <summary>The in-memory, editable representation of a long-form document.</summary>
public sealed class StructuredDocument
{
    public string Title { get; set; } = "Untitled";
    public PageSetup Page { get; set; } = new();
    public StyleSheet Styles { get; set; } = StyleSheet.CreateDefault();
    public List<Block> Blocks { get; set; } = new();

    /// <summary>Returns the distinct paragraph style names the blocks will use.</summary>
    public IEnumerable<string> UsedParagraphStyles()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in Blocks)
        {
            if (block.Kind == BlockKind.PageBreak) continue;
            if (block.Kind == BlockKind.Table)
            {
                names.Add(StyleSheet.TableHeaderStyle);
                names.Add(StyleSheet.TableBodyStyle);
            }
            names.Add(Styles.ResolveParagraphStyle(block));
        }
        return names;
    }
}
