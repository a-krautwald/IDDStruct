namespace IDDStruct.Core.Model;

/// <summary>Page geometry in PostScript points (1 pt = 1/72 inch).</summary>
public sealed class PageSetup
{
    public const double PointsPerMillimetre = 72.0 / 25.4;

    public double Width { get; set; } = 210 * PointsPerMillimetre;
    public double Height { get; set; } = 297 * PointsPerMillimetre;
    public double MarginTop { get; set; } = 20 * PointsPerMillimetre;
    public double MarginBottom { get; set; } = 25 * PointsPerMillimetre;
    public double MarginInside { get; set; } = 20 * PointsPerMillimetre;
    public double MarginOutside { get; set; } = 20 * PointsPerMillimetre;
    public int Columns { get; set; } = 1;
    public double ColumnGutter { get; set; } = 12;

    public double TextAreaWidth => Width - MarginInside - MarginOutside;
    public double TextAreaHeight => Height - MarginTop - MarginBottom;

    public static IReadOnlyDictionary<string, (double WidthMm, double HeightMm)> Presets { get; } =
        new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase)
        {
            ["A4"] = (210, 297),
            ["A5"] = (148, 210),
            ["A3"] = (297, 420),
            ["Letter"] = (215.9, 279.4),
            ["Legal"] = (215.9, 355.6),
        };

    public static PageSetup FromPreset(string name)
    {
        if (!Presets.TryGetValue(name, out var size))
            throw new ArgumentException($"Unknown page size '{name}'. Known sizes: {string.Join(", ", Presets.Keys)}.", nameof(name));
        return new PageSetup
        {
            Width = size.WidthMm * PointsPerMillimetre,
            Height = size.HeightMm * PointsPerMillimetre,
        };
    }

    /// <summary>Returns the preset name matching this page size, or null for a custom size.</summary>
    public string? PresetName => Presets
        .Where(p => Math.Abs(p.Value.WidthMm * PointsPerMillimetre - Width) < 0.5
                 && Math.Abs(p.Value.HeightMm * PointsPerMillimetre - Height) < 0.5)
        .Select(p => p.Key)
        .FirstOrDefault();
}
