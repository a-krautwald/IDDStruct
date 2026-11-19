namespace IDDStruct.Core.Import;

/// <summary>Reads one source format into a <see cref="ImportResult"/>.</summary>
public interface IDataImporter
{
    string DisplayName { get; }

    /// <summary>Extensions handled, including the dot (e.g. ".csv").</summary>
    IReadOnlyList<string> Extensions { get; }

    ImportResult Import(Stream source, string sourceName, ImportOptions options);
}
