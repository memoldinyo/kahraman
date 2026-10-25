namespace Kahraman.Core;

public static class Recent
{
    /// <summary>Die zuletzt benutzten Tätigkeiten (neueste zuerst, ohne Duplikate).</summary>
    public static List<string> Activities(IReadOnlyList<LogEntry> entries, int count) =>
        Distinct(entries, count, e => e.Activity, _ => true);

    /// <summary>Die zuletzt benutzten Untertätigkeiten einer Tätigkeit.</summary>
    public static List<string> Subs(IReadOnlyList<LogEntry> entries, string activity, int count) =>
        Distinct(entries, count, e => e.Sub,
            e => string.Equals(e.Activity.Trim(), activity.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Kommentar des letzten Eintrags mit dieser Tätigkeit und Untertätigkeit ("" wenn es keinen gibt).</summary>
    public static string Comment(IReadOnlyList<LogEntry> entries, string activity, string sub)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (string.Equals(e.Activity.Trim(), activity.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.Sub.Trim(), sub.Trim(), StringComparison.OrdinalIgnoreCase))
                return e.Comment;
        }
        return "";
    }

    static List<string> Distinct(IReadOnlyList<LogEntry> entries, int count, Func<LogEntry, string> pick, Func<LogEntry, bool> where)
    {
        var result = new List<string>();
        if (count <= 0) return result;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = entries.Count - 1; i >= 0 && result.Count < count; i--)
        {
            var e = entries[i];
            if (!where(e)) continue;
            var v = pick(e);
            if (v.Trim().Length == 0) continue;
            if (seen.Add(v.Trim())) result.Add(v);
        }
        return result;
    }
}

/// <summary>Untertätigkeiten aus einer Textdatei: erste Zeile = Tätigkeit (Anfang genügt), jede weitere Zeile = eine Untertätigkeit.</summary>
public static class SubImport
{
    public static (string Activity, List<string> Subs) Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
        if (lines.Count == 0) throw new KahramanException(L.T("file.empty"));
        var subs = lines.Skip(1).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return (lines[0], subs);
    }

    /// <summary>Sucht die Tätigkeit: exakt, sonst alle, die mit dem Text beginnen.</summary>
    public static List<Activity> Candidates(ActivityStore store, string name)
    {
        if (store.Find(name) is { } exact) return [exact];
        return store.Activities.Values
            .Where(a => a.Text.Trim().StartsWith(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
