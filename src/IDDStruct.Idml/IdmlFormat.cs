using System.Globalization;
using System.Text;
using System.Xml;

namespace IDDStruct.Idml;

/// <summary>Shared IDML constants and XML helpers.</summary>
internal static class IdmlFormat
{
    public const string PackagingNs = "http://ns.adobe.com/AdobeInDesign/idml/1.0/packaging";
    public const string MimeType = "application/vnd.adobe.indesign-idml-package";

    /// <summary>DOM version 8.0 (CS6) opens in every later InDesign release.</summary>
    public const string DomVersion = "8.0";

    public const string NoParagraphStyle = "ParagraphStyle/$ID/[No paragraph style]";
    public const string NormalParagraphStyle = "ParagraphStyle/$ID/NormalParagraphStyle";
    public const string NoCharacterStyle = "CharacterStyle/$ID/[No character style]";

    public static string Num(double value) =>
        Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>Escapes a style name for use in a Self/reference ID (InDesign encodes ':' as %3a).</summary>
    public static string EscapeName(string name) => name.Replace("%", "%25").Replace(":", "%3a");

    public static string ParagraphStyleRef(string name) => "ParagraphStyle/" + EscapeName(name);
    public static string CharacterStyleRef(string name) =>
        string.IsNullOrEmpty(name) ? NoCharacterStyle : "CharacterStyle/" + EscapeName(name);

    public static XmlWriter CreateWriter(Stream stream) => XmlWriter.Create(stream, new XmlWriterSettings
    {
        Encoding = new UTF8Encoding(false),
        Indent = true,
        IndentChars = "\t",
        CloseOutput = false,
    });

    /// <summary>Starts a package part: XML declaration and &lt;idPkg:{part}&gt; root element.</summary>
    public static void StartPart(XmlWriter w, string part)
    {
        w.WriteStartDocument(standalone: true);
        w.WriteStartElement("idPkg", part, PackagingNs);
        w.WriteAttributeString("DOMVersion", DomVersion);
    }

    public static void EndPart(XmlWriter w)
    {
        w.WriteEndElement();
        w.WriteEndDocument();
    }

    public static void Attr(XmlWriter w, string name, double value) => w.WriteAttributeString(name, Num(value));
    public static void Attr(XmlWriter w, string name, string value) => w.WriteAttributeString(name, value);
    public static void Attr(XmlWriter w, string name, bool value) => w.WriteAttributeString(name, value ? "true" : "false");
}

/// <summary>Generates unique Self IDs for IDML objects.</summary>
internal sealed class IdGenerator
{
    private int _next = 0x100;
    public string Next(string prefix = "u") => prefix + (_next++).ToString("x", CultureInfo.InvariantCulture);
}
