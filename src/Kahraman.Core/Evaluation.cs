using System.Globalization;
using System.Text;

namespace Kahraman.Core;

public enum PeriodKind { Heute, Woche, Monat, Quartal, Jahr, Frei }

public enum DetailLevel { Taetigkeiten, Untertaetigkeiten, Kommentare }

/// <summary>Zeitraum aus ganzen Tagen, beide Grenzen einschließlich.</summary>
public readonly record struct Period(DateOnly From, DateOnly To)
{
    public static Period Of(PeriodKind kind, DateOnly day) => kind switch
    {
        PeriodKind.Heute or PeriodKind.Frei => new(day, day),
        PeriodKind.Woche => Week(day),
        PeriodKind.Monat => new(new DateOnly(day.Year, day.Month, 1), new DateOnly(day.Year, day.Month, 1).AddMonths(1).AddDays(-1)),
        PeriodKind.Quartal => Quarter(day),
        PeriodKind.Jahr => new(new DateOnly(day.Year, 1, 1), new DateOnly(day.Year, 12, 31)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    static Period Week(DateOnly day)
    {
        int offset = ((int)day.DayOfWeek + 6) % 7; // Woche beginnt am Montag
        var monday = day.AddDays(-offset);
        return new(monday, monday.AddDays(6));
    }

    static Period Quarter(DateOnly day)
    {
        var first = new DateOnly(day.Year, (day.Month - 1) / 3 * 3 + 1, 1);
        return new(first, first.AddMonths(3).AddDays(-1));
    }

    /// <summary>Zur nächsten (+1) oder vorherigen (-1) Periode wechseln.</summary>
    public Period Shift(PeriodKind kind, int direction) => kind switch
    {
        PeriodKind.Heute => new(From.AddDays(direction), To.AddDays(direction)),
        PeriodKind.Woche => new(From.AddDays(7 * direction), To.AddDays(7 * direction)),
        PeriodKind.Monat => Of(kind, From.AddMonths(direction)),
        PeriodKind.Quartal => Of(kind, From.AddMonths(3 * direction)),
        PeriodKind.Jahr => Of(kind, From.AddYears(direction)),
        _ => new(From.AddDays((To.DayNumber - From.DayNumber + 1) * direction), To.AddDays((To.DayNumber - From.DayNumber + 1) * direction)),
    };

    public bool Contains(DateTime t) => DateOnly.FromDateTime(t) >= From && DateOnly.FromDateTime(t) <= To;
}

/// <summary>Zeitspanne, die einem Eintrag zugerechnet wird.</summary>
public readonly record struct Span(LogEntry Entry, TimeSpan Duration)
{
    public DateOnly Day => DateOnly.FromDateTime(Entry.Time);
}

public sealed record EvaluationRow(string Activity, string Sub, string Comment, double Hours)
{
    public double Days => Hours / Evaluation.HoursPerDay;
}

public sealed record DetailRow(DateOnly Day, string Activity, string Sub, string Comment, double Hours);

public static class Evaluation
{
    public const double HoursPerDay = 8.0;

    /// <summary>
    /// Ein Eintrag beschreibt die Zeit seit dem vorherigen Eintrag. Der erste Eintrag eines Tages
    /// (nach Systemzeit-Mitternacht) markiert nur die Startzeit und zählt nicht.
    /// </summary>
    public static List<Span> Spans(IReadOnlyList<LogEntry> entries)
    {
        var spans = new List<Span>(entries.Count);
        for (int i = 1; i < entries.Count; i++)
        {
            var prev = entries[i - 1];
            var cur = entries[i];
            if (cur.Time.Date != prev.Time.Date) continue;
            var d = cur.Time - prev.Time;
            if (d > TimeSpan.Zero) spans.Add(new Span(cur, d));
        }
        return spans;
    }

    public static string Key(string s) => s.Trim();

    static IEnumerable<Span> Filter(IEnumerable<Span> spans, Period period, ISet<string> excluded) =>
        spans.Where(s => period.Contains(s.Entry.Time) && !excluded.Contains(Key(s.Entry.Activity)));

    public static ISet<string> ExcludedSet(IEnumerable<string> excluded) =>
        new HashSet<string>(excluded.Select(Key), StringComparer.OrdinalIgnoreCase);

    public static List<EvaluationRow> Aggregate(IEnumerable<Span> spans, Period period, DetailLevel level, ISet<string> excluded)
    {
        var groups = new Dictionary<(string, string, string), (EvaluationRow Names, double Hours)>(new KeyComparer());
        foreach (var s in Filter(spans, period, excluded))
        {
            var e = s.Entry;
            var key = (Key(e.Activity),
                level >= DetailLevel.Untertaetigkeiten ? Key(e.Sub) : "",
                level >= DetailLevel.Kommentare ? Key(e.Comment) : "");
            groups.TryGetValue(key, out var g);
            groups[key] = (g.Names ?? new EvaluationRow(key.Item1, key.Item2, key.Item3, 0), g.Hours + s.Duration.TotalHours);
        }
        return groups.Values
            .Select(g => g.Names with { Hours = g.Hours })
            .OrderBy(r => r.Activity, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Sub, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Comment, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Tageweise Aufstellung nach Tätigkeit, Untertätigkeit und Kommentar.</summary>
    /// <param name="activities">Nur diese Tätigkeiten; leer = alle.</param>
    public static List<DetailRow> Details(IEnumerable<Span> spans, Period period, ISet<string> excluded, IReadOnlyCollection<string> activities)
    {
        var only = new HashSet<string>(activities.Select(Key), StringComparer.OrdinalIgnoreCase);
        var groups = new Dictionary<(DateOnly, string, string, string), double>();
        var cmp = new KeyComparer();
        var canonical = new Dictionary<(string, string, string), (string, string, string)>(cmp);
        foreach (var s in Filter(spans, period, excluded))
        {
            var e = s.Entry;
            var names = (Key(e.Activity), Key(e.Sub), Key(e.Comment));
            if (only.Count > 0 && !only.Contains(names.Item1)) continue;
            if (!canonical.TryGetValue(names, out var c)) canonical[names] = c = names;
            var key = (s.Day, c.Item1, c.Item2, c.Item3);
            groups[key] = groups.GetValueOrDefault(key) + s.Duration.TotalHours;
        }
        return groups
            .Select(g => new DetailRow(g.Key.Item1, g.Key.Item2, g.Key.Item3, g.Key.Item4, g.Value))
            .OrderBy(r => r.Day)
            .ThenBy(r => r.Activity, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Sub, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Comment, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string FormatHours(double hours, CultureInfo? culture = null) =>
        hours.ToString("0.00", culture ?? CultureInfo.CurrentCulture);

    public static string DetailsAsText(IEnumerable<DetailRow> rows, CultureInfo? culture = null)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join("\t", L.T("col.date"), L.T("col.hours"), L.T("col.activity"), L.T("col.sub"), L.T("col.comment")) + "\r\n");
        foreach (var r in rows)
            sb.Append($"{r.Day:yyyy-MM-dd}\t{FormatHours(r.Hours, culture)}\t{r.Activity}\t{r.Sub}\t{r.Comment}\r\n");
        return sb.ToString();
    }

    sealed class KeyComparer : IEqualityComparer<(string, string, string)>
    {
        static readonly StringComparer C = StringComparer.OrdinalIgnoreCase;
        public bool Equals((string, string, string) x, (string, string, string) y) =>
            C.Equals(x.Item1, y.Item1) && C.Equals(x.Item2, y.Item2) && C.Equals(x.Item3, y.Item3);
        public int GetHashCode((string, string, string) k) =>
            HashCode.Combine(C.GetHashCode(k.Item1), C.GetHashCode(k.Item2), C.GetHashCode(k.Item3));
    }
}
