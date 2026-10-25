using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kahraman;

public sealed record WindowPlacement(int X, int Y, int Width, int Height)
{
    public Rectangle ToRectangle() => new(X, Y, Width, Height);
    public static WindowPlacement From(Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
}

public enum ThemeMode { System, Light, Dark }

/// <summary>Einstellungen, gespeichert als JSON in %APPDATA%\Kahraman\settings.json.</summary>
public sealed class Settings
{
    /// <summary>Sprache der Oberfläche ("de", "en", ...); leer = Sprache von Windows.</summary>
    public string? Language { get; set; }
    /// <summary>Hell, dunkel oder wie Windows.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ThemeMode>))]
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    /// <summary>Verzeichnis mit taetigkeiten.kahraman und dem Logfile.</summary>
    public string? DataDir { get; set; }
    /// <summary>Nachfrage-Intervall in Minuten (1 Minute bis 24 h).</summary>
    public int IntervalMinutes { get; set; } = 20;
    /// <summary>Anzahl zuletzt benutzter Einträge oben in den Auswahllisten.</summary>
    public int RecentCount { get; set; } = 5;
    public List<string> ExcludedActivities { get; set; } = [];
    public Keys HotkeyFinish { get; set; }
    public Keys HotkeyEdit { get; set; }
    public bool Autostart { get; set; }
    /// <summary>Kommentarfeld im Eingabefenster nur aufklappen, wenn ein Kommentar darin steht.</summary>
    public bool CommentOnlyWhenFilled { get; set; } = true;
    /// <summary>Untertätigkeiten zuletzt von Hand aufgeklappt: dann immer zeigen, sonst nur, wenn eine vorgeschlagen wird.</summary>
    public bool SubsExpanded { get; set; }
    /// <summary>Abfrage darf minimiert werden (blinkt dann in der Taskleiste).</summary>
    public bool AllowMinimize { get; set; }
    /// <summary>Unbeantwortete Abfrage: Titelleiste blinkt.</summary>
    public bool FlashCaption { get; set; }
    /// <summary>Unbeantwortete Abfrage: Symbol in der Taskleiste blinkt.</summary>
    public bool FlashTaskbar { get; set; }
    /// <summary>Unbeantwortete Abfrage rüttelt sich regelmäßig, wenn man woanders arbeitet.</summary>
    public bool Shake { get; set; }
    /// <summary>Abstand zwischen zwei Rüttel-Erinnerungen in Minuten (1 bis 60).</summary>
    public int ShakeMinutes { get; set; } = 2;
    /// <summary>Beim Rütteln einen Warnton spielen.</summary>
    public bool ShakeSound { get; set; } = true;
    public Dictionary<string, WindowPlacement> Windows { get; set; } = [];

    [JsonIgnore] public string FilePath { get; private set; } = DefaultFile;

    public static string DefaultFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kahraman", "settings.json");

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static Settings Load(string file)
    {
        Settings s;
        if (File.Exists(file))
        {
            try { s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(file), Json) ?? new(); }
            catch (JsonException) { s = new(); }
        }
        else
        {
            s = new();
        }
        s.FilePath = file;
        s.IntervalMinutes = Math.Clamp(s.IntervalMinutes, 1, 24 * 60);
        s.RecentCount = Math.Clamp(s.RecentCount, 0, 50);
        s.ShakeMinutes = Math.Clamp(s.ShakeMinutes, 1, 60);
        return s;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
    }

    public void Remember(Form form, string? key = null)
    {
        var r = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
        Windows[key ?? form.GetType().Name] = WindowPlacement.From(r);
        Save();
    }

    /// <summary>Stellt die gespeicherte Position wieder her, sofern sie auf einem Bildschirm sichtbar ist.</summary>
    public void Restore(Form form, string? key = null)
    {
        if (!Windows.TryGetValue(key ?? form.GetType().Name, out var p)) return;
        var r = p.ToRectangle();
        if (r.Width < 100 || r.Height < 80) return;
        if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(new Rectangle(r.X, r.Y, Math.Min(r.Width, 200), 40)))) return;
        form.StartPosition = FormStartPosition.Manual;
        form.Bounds = r;
    }
}
