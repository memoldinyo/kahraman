using Kahraman.Core;

namespace Kahraman;

/// <summary>Gemeinsamer Zustand: Einstellungen, Tätigkeiten-Datei und Logfile.</summary>
public sealed class AppServices(Settings settings)
{
    public Settings Settings { get; } = settings;
    public ActivityStore Store { get; private set; } = null!;
    public LogFile Log { get; private set; } = null!;

    public event Action? DataChanged;

    public string DataDir => Settings.DataDir ?? throw new InvalidOperationException("DataDir");
    public string StorePath => DataFiles.StorePath(DataDir);
    public string LogPath => DataFiles.LogPath(DataDir, Environment.UserName);

    /// <summary>Öffnet die Dateien im eingestellten Verzeichnis. Fehlende Dateien werden (nach Rückfrage) angelegt.</summary>
    public bool Open(IWin32Window? owner)
    {
        if (string.IsNullOrEmpty(Settings.DataDir)) return false;
        Directory.CreateDirectory(DataDir);
        if (!File.Exists(StorePath))
        {
            var answer = Ui.Msg.Show(owner, L.F("data.startNew", DataDir), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return false;
            Store = ActivityStore.CreateNew(StorePath, L.T("data.firstActivity"));
        }
        else
        {
            Store = ActivityStore.Load(StorePath);
        }
        Log = LogFile.Load(LogPath);
        DataChanged?.Invoke();
        return true;
    }

    public void Refresh()
    {
        if (Store.ChangedOnDisk()) Store.Reload();
        Log.ReloadIfChanged();
    }

    public void Record(LogEntry entry)
    {
        Log.Append(entry);
        DataChanged?.Invoke();
    }

    public void SaveLog(IReadOnlyList<LogEntry> entries)
    {
        Log.Save(entries);
        DataChanged?.Invoke();
    }

    public void ModifyStore(Action<ActivityStore> change)
    {
        Store.Modify(change);
        DataChanged?.Invoke();
    }

    public int StatusOf(string activity) => Store.Find(activity)?.Status ?? 0;

    public int StatusOf(string activity, string sub) =>
        Store.Find(activity)?.FindSub(sub)?.Status ?? (Store.Defaults.FirstOrDefault(d => d.Text == sub)?.Status ?? 0);

    /// <summary>Alle Tätigkeitsnamen aus Tätigkeiten-Datei und Logfile (für die Auswertungseinstellungen).</summary>
    public List<string> AllActivityNames()
    {
        var names = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var a in Store.Activities.Values) names.Add(a.Text.Trim());
        foreach (var e in Log.Entries) if (e.Activity.Trim().Length > 0) names.Add(e.Activity.Trim());
        return names.ToList();
    }
}
