using IDDStruct.Core.Model;

namespace IDDStruct.Core.Import;

public sealed class ImportResult
{
    public ImportResult(StructuredDocument document) => Document = document;

    public StructuredDocument Document { get; }
    public List<string> Warnings { get; } = new();
}
