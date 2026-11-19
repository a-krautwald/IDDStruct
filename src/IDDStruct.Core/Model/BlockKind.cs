namespace IDDStruct.Core.Model;

/// <summary>The structural role of a block in a long-form document.</summary>
public enum BlockKind
{
    Heading,
    Paragraph,
    BulletItem,
    NumberedItem,
    Table,
    PageBreak,
}
