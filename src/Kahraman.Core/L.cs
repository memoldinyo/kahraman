using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Kahraman.Core;

/// <summary>Eine Sprache der Oberfläche.</summary>
/// <param name="Code">Zweibuchstabiger ISO-Code, zugleich Endung der Sprachdateien (ui.de.json, quips.de.json, help.de.txt).</param>
/// <param name="NativeName">Name der Sprache in der Sprache selbst (für die Auswahl in den Einstellungen).</param>
public sealed record Language(string Code, string NativeName, bool RightToLeft = false);

/// <summary>
/// Übersetzte Texte. Die Sprachdateien liegen eingebettet unter i18n/. Fehlt ein Text in der gewählten Sprache,
/// wird der englische, dann der deutsche genommen.
/// </summary>
public static class L
{
    public static readonly Language[] All =
    [
        new("de", "Deutsch"),
        new("en", "English"),
        new("tr", "Türkçe"),
        new("fr", "Français"),
        new("it", "Italiano"),
        new("ar", "العربية", RightToLeft: true),
        new("zh", "中文（简体）"),
        new("ru", "Русский"),
    ];

    static readonly Dictionary<string, Dictionary<string, string>> cache = [];
    static Dictionary<string, string> current = [], english = [], german = [];

    public static Language Current { get; private set; } = All[0];

    static L() => Use(null);

    /// <summary>Stellt die Sprache ein; leer oder null = Sprache von Windows (nicht unterstützt: Englisch).</summary>
    public static void Use(string? code)
    {
        Current = Resolve(code);
        german = Texts("de");
        english = Texts("en");
        current = Texts(Current.Code);
    }

    public static Language Resolve(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return All.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
               ?? All.First(l => l.Code == "en");
    }

    /// <summary>Text zum Schlüssel in der eingestellten Sprache.</summary>
    public static string T(string key) =>
        current.TryGetValue(key, out var s) || english.TryGetValue(key, out s) || german.TryGetValue(key, out s) ? s : key;

    /// <summary>Text mit Platzhaltern {0}, {1} ... (Zahlen und Daten im Format von Windows).</summary>
    public static string F(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);

    /// <summary>Alle Texte einer Sprache (für Tests und die Fallback-Kette).</summary>
    public static Dictionary<string, string> Texts(string code)
    {
        lock (cache)
        {
            if (cache.TryGetValue(code, out var d)) return d;
            d = [];
            if (Resource($"ui.{code}.json") is { } json)
                foreach (var p in JsonDocument.Parse(json).RootElement.EnumerateObject())
                    d[p.Name] = p.Value.GetString() ?? "";
            return cache[code] = d;
        }
    }

    /// <summary>Eingebettete Sprachdatei (z. B. "help.tr.txt"), null wenn es sie nicht gibt.</summary>
    public static string? Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Kahraman.i18n." + name);
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Sprachdatei der eingestellten Sprache, sonst Englisch, sonst Deutsch.</summary>
    public static string LocalizedResource(string baseName, string extension) =>
        Resource($"{baseName}.{Current.Code}.{extension}") ?? Resource($"{baseName}.en.{extension}") ?? Resource($"{baseName}.de.{extension}") ?? "";
}
