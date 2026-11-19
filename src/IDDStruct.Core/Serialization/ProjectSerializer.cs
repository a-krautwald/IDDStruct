using System.Text.Json;
using System.Text.Json.Serialization;
using IDDStruct.Core.Model;

namespace IDDStruct.Core.Serialization;

/// <summary>Saves and loads the app's native project format (<c>.iddstruct</c>, JSON).</summary>
public static class ProjectSerializer
{
    public const string FileExtension = ".iddstruct";
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private sealed class Envelope
    {
        public string Format { get; set; } = "iddstruct";
        public int Version { get; set; } = FormatVersion;
        public StructuredDocument Document { get; set; } = new();
    }

    public static string Serialize(StructuredDocument document) =>
        JsonSerializer.Serialize(new Envelope { Document = document }, Options);

    public static StructuredDocument Deserialize(string json)
    {
        var envelope = JsonSerializer.Deserialize<Envelope>(json, Options)
            ?? throw new InvalidDataException("Project file is empty.");
        if (envelope.Format != "iddstruct")
            throw new InvalidDataException("Not an IDDStruct project file.");
        if (envelope.Version > FormatVersion)
            throw new InvalidDataException($"Project file version {envelope.Version} is newer than this app supports ({FormatVersion}).");
        return envelope.Document;
    }

    public static void Save(StructuredDocument document, string path) =>
        File.WriteAllText(path, Serialize(document));

    public static StructuredDocument Load(string path) => Deserialize(File.ReadAllText(path));
}
