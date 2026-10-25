namespace Kahraman.Core;

public class KahramanException(string message) : Exception(message);

/// <summary>Das Logfile "benutzername.kahraman": chronologische Liste der Einträge.</summary>
public sealed class LogFile
{
    DateTime loadedWriteTime;
    long loadedLength = -1;

    public LogFile(string path) => Path = path;

    public string Path { get; }
    public List<LogEntry> Entries { get; private set; } = [];
    /// <summary>Zeilen, die nicht gelesen werden konnten. Solange es welche gibt, wird die Datei nie neu geschrieben.</summary>
    public List<string> BadLines { get; private set; } = [];
    public LogEntry? Last => Entries.Count > 0 ? Entries[^1] : null;

    public static LogFile Load(string path)
    {
        var log = new LogFile(path);
        log.Reload();
        return log;
    }

    public void Reload()
    {
        var entries = new List<LogEntry>();
        var bad = new List<string>();
        if (File.Exists(Path))
        {
            var info = new FileInfo(Path);
            loadedWriteTime = info.LastWriteTimeUtc;
            loadedLength = info.Length;
            foreach (var line in ReadLines(File.ReadAllBytes(Path)))
            {
                if (line.Trim().Length == 0) continue;
                if (LogEntry.TryParse(line, out var e)) entries.Add(e!);
                else bad.Add(line);
            }
        }
        else
        {
            loadedWriteTime = default;
            loadedLength = -1;
        }
        // Die Datei ist normalerweise sortiert; zur Sicherheit stabil sortieren.
        Entries = entries.OrderBy(e => e.Time).ToList();
        BadLines = bad;
    }

    public bool ChangedOnDisk()
    {
        if (!File.Exists(Path)) return loadedLength != -1;
        var info = new FileInfo(Path);
        return info.Length != loadedLength || info.LastWriteTimeUtc != loadedWriteTime;
    }

    public void ReloadIfChanged()
    {
        if (ChangedOnDisk()) Reload();
    }

    static IEnumerable<string> ReadLines(byte[] data)
    {
        int start = 0;
        for (int i = 0; i <= data.Length; i++)
        {
            if (i == data.Length || data[i] == (byte)'\n')
            {
                int end = i;
                if (end > start && data[end - 1] == (byte)'\r') end--;
                if (end > start || i < data.Length)
                    yield return TextCodec.DecodeLine(data.AsSpan(start, end - start));
                start = i + 1;
            }
        }
    }

    /// <summary>Hängt einen Eintrag an. Er muss zeitlich nach dem letzten Eintrag liegen.</summary>
    public void Append(LogEntry entry)
    {
        ReloadIfChanged();
        if (Last is { } last && entry.Time <= last.Time)
            throw new KahramanException(entry.Time == last.Time
                ? L.T("log.sameTime")
                : L.F("log.beforeLast", entry.Time, last.Time));

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        using (var fs = new FileStream(Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
        {
            bool needsNewline = false;
            if (fs.Length > 0)
            {
                fs.Seek(-1, SeekOrigin.End);
                needsNewline = fs.ReadByte() != '\n';
            }
            fs.Seek(0, SeekOrigin.End);
            if (needsNewline) fs.Write("\r\n"u8);
            fs.Write(TextCodec.EncodeLine(entry.ToLine()));
            fs.Write("\r\n"u8);
        }
        Entries.Add(entry);
        var info = new FileInfo(Path);
        loadedWriteTime = info.LastWriteTimeUtc;
        loadedLength = info.Length;
    }

    /// <summary>Schreibt die Datei komplett neu (nach Bearbeitung/Import). Die alte Fassung bleibt als .bak erhalten.</summary>
    public void Save(IReadOnlyList<LogEntry> entries)
    {
        if (BadLines.Count > 0)
            throw new KahramanException(L.F("log.badLines", BadLines.Count, BadLines[0]));
        // Zeitgleiche Einträge kommen in alten Logfiles vereinzelt vor und bleiben erlaubt.
        for (int i = 1; i < entries.Count; i++)
            if (entries[i].Time < entries[i - 1].Time)
                throw new KahramanException(L.F("log.notChronological", entries[i].Time));

        var tmp = Path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
        {
            foreach (var e in entries)
            {
                fs.Write(TextCodec.EncodeLine(e.ToLine()));
                fs.Write("\r\n"u8);
            }
        }
        if (File.Exists(Path)) File.Replace(tmp, Path, Path + ".bak");
        else File.Move(tmp, Path);
        Reload();
    }

    /// <summary>Fügt importierte Einträge chronologisch ein. Zeitgleiche Einträge werden verweigert.</summary>
    public static List<LogEntry> Merge(IReadOnlyList<LogEntry> existing, IEnumerable<LogEntry> imported, out List<LogEntry> conflicts)
    {
        var times = existing.Select(e => e.Time).ToHashSet();
        conflicts = [];
        var merged = new List<LogEntry>(existing);
        foreach (var e in imported)
        {
            if (!times.Add(e.Time)) conflicts.Add(e);
            else merged.Add(e);
        }
        merged.Sort((a, b) => a.Time.CompareTo(b.Time));
        return merged;
    }
}
