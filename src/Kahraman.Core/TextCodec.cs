using System.Text;

namespace Kahraman.Core;

/// <summary>
/// Kodierung der Kahraman-Dateien: ANSI (Windows-1252).
/// Zeilen mit Zeichen außerhalb von 1252 (z. B. türkische Buchstaben) werden als UTF-8 geschrieben
/// und beim Lesen zeilenweise erkannt.
/// </summary>
public static class TextCodec
{
    static readonly Encoding ansiStrict;
    static readonly Encoding utf8Strict = new UTF8Encoding(false, true);

    static TextCodec()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Ansi = Encoding.GetEncoding(1252);
        ansiStrict = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback);
    }

    public static Encoding Ansi { get; }

    public static bool IsAnsiEncodable(string s)
    {
        try { ansiStrict.GetByteCount(s); return true; }
        catch (EncoderFallbackException) { return false; }
    }

    public static byte[] EncodeLine(string s) =>
        IsAnsiEncodable(s) ? Ansi.GetBytes(s) : Encoding.UTF8.GetBytes(s);

    public static string DecodeLine(ReadOnlySpan<byte> bytes)
    {
        bool ascii = true;
        foreach (var b in bytes) if (b >= 0x80) { ascii = false; break; }
        if (!ascii)
        {
            try { return utf8Strict.GetString(bytes); }
            catch (DecoderFallbackException) { }
        }
        return Ansi.GetString(bytes);
    }

    /// <summary>Text-Datei mit unbekannter Kodierung lesen (UTF-8 mit/ohne BOM, sonst ANSI).</summary>
    public static string ReadAllTextGuess(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        try { return utf8Strict.GetString(bytes); }
        catch (DecoderFallbackException) { return Ansi.GetString(bytes); }
    }
}
