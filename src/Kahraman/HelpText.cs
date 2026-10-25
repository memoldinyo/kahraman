using Kahraman.Core;
using Kahraman.Ui;

namespace Kahraman;

public static class HelpText
{
    public static Settings? Settings { get; set; }

    /// <summary>Die Hilfe steht pro Sprache in i18n/help.xx.txt.</summary>
    public static string Help => L.LocalizedResource("help", "txt");

    public static void Show(IWin32Window? owner) => ShowText(owner, L.T("help.title"), Help, "Help");

    public static void License(IWin32Window? owner) => ShowText(owner, L.T("license.title"), LicenseText, "License");

    /// <summary>Die Apache-Lizenz 2.0 (Datei LICENSE) ist in die Programmdatei eingebettet; es gibt sie nur auf Englisch.</summary>
    public static string LicenseText => Embedded("Kahraman.LICENSE.txt") ?? L.T("license.missing");

    static string? Embedded(string name)
    {
        using var stream = typeof(HelpText).Assembly.GetManifestResourceStream(name);
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static void About(IWin32Window? owner, AppServices app)
    {
        var version = typeof(HelpText).Assembly.GetName().Version?.ToString(3);
        var entries = app.Log is null ? "-" : $"{app.LogPath} ({app.Log.Entries.Count})";
        var text = L.F("about.text", version, app.Settings.DataDir, entries, app.Settings.FilePath);
        ShowText(owner, L.T("about.title"), text, "About", new Size(640, 330));
    }

    static void ShowText(IWin32Window? owner, string title, string text, string key, Size? size = null)
    {
        var f = new TextForm(title, text.ReplaceLineEndings("\r\n"), Settings ?? new Settings(), saveable: false, key: key);
        if (size is { } s) f.Size = s;
        if (owner is Form o) f.Show(o); else f.Show();
    }
}
