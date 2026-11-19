using IDDStruct.Core.Import;
using IDDStruct.Idml;

namespace IDDStruct.Tests;

/// <summary>Smoke-tests every file in /samples through import and IDML export.</summary>
public class SampleFileTests
{
    private static string SamplesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "samples"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("samples folder not found"), "samples");
    }

    public static IEnumerable<object[]> Samples() =>
        Directory.GetFiles(SamplesDir()).Where(f => DocumentImporter.FindImporter(f) is not null).Select(f => new object[] { Path.GetFileName(f) });

    [Theory]
    [MemberData(nameof(Samples))]
    public void SampleImportsAndExports(string fileName)
    {
        var result = DocumentImporter.Import(Path.Combine(SamplesDir(), fileName));
        Assert.NotEmpty(result.Document.Blocks);
        using var ms = new MemoryStream();
        var report = new IdmlExporter().Export(result.Document, ms);
        Assert.True(report.PageCount >= 1);
        Assert.True(ms.Length > 1000);
    }
}
