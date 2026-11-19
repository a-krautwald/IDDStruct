namespace IDDStruct.Core.Import;

public enum DataNodeKind { Scalar, Object, Array }

/// <summary>
/// A format-neutral tree used for hierarchical sources (JSON and XML), so a single mapper can
/// turn either into document structure. Scalar values hold inline markup.
/// </summary>
public sealed class DataNode
{
    public DataNode(string name, DataNodeKind kind, string? value = null)
    {
        Name = name;
        Kind = kind;
        Value = value;
    }

    public string Name { get; }
    public DataNodeKind Kind { get; }
    public string? Value { get; }
    public List<DataNode> Children { get; } = new();
    public Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsScalar => Kind == DataNodeKind.Scalar;

    /// <summary>Finds a scalar child or attribute by any of the given names (case-insensitive).</summary>
    public string? GetText(params string[] names)
    {
        foreach (var name in names)
        {
            var child = Children.FirstOrDefault(c => c.IsScalar && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (child is not null) return child.Value;
            if (Attributes.TryGetValue(name, out var attr)) return attr;
        }
        return null;
    }

    public DataNode? GetChild(params string[] names) =>
        Children.FirstOrDefault(c => names.Any(n => string.Equals(c.Name, n, StringComparison.OrdinalIgnoreCase)));

    public override string ToString() => $"{Kind} {Name}" + (Value is null ? "" : $" = {Value}");
}
