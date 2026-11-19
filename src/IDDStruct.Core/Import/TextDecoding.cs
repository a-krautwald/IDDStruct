using System.Text;

namespace IDDStruct.Core.Import;

internal static class TextDecoding
{
    /// <summary>
    /// Decodes text honouring a BOM; otherwise tries strict UTF-8 and falls back to Latin-1
    /// (covers legacy Windows exports with æ, ø, å etc.).
    /// </summary>
    public static string Decode(byte[] bytes, out bool usedFallback)
    {
        usedFallback = false;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            usedFallback = true;
            return Encoding.Latin1.GetString(bytes);
        }
    }

    public static byte[] ReadAll(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
