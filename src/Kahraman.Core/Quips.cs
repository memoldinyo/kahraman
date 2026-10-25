using System.Text.Json;

namespace Kahraman.Core;

/// <summary>
/// Kahraman fragt nicht immer gleich: Frage bzw. Spruch, Fenstertitel und OK-Knopf werden zufällig
/// und gewichtet gewählt. Manche Sprüche passen nur zu bestimmten Tagen oder Uhrzeiten.
/// Die Texte stehen pro Sprache in i18n/quips.xx.json.
/// </summary>
public static class Quips
{
    /// <summary>Höchstbreite einer Frage (in Zeichen, chinesische Zeichen zählen doppelt), damit sie in eine Zeile passt.</summary>
    public const int MaxQuestionLength = 80;
    /// <summary>Höchstbreite der OK-Beschriftung, damit der Knopf seine Größe behält.</summary>
    public const int MaxOkLength = 12;
    const int MaxLastLength = 30;
    public const int FactWeight = 2;

    /// <param name="IsFact">Wissensfrage: bekommt beim Anzeigen eine Einleitung und eine Anschlussfrage.</param>
    public sealed record Quip(int Weight, string Text, Func<DateTime, bool>? When = null, bool IsFact = false);

    /// <summary>Alle Sprüche einer Sprache.</summary>
    public sealed class Set
    {
        public Quip[] Questions { get; init; } = [];
        /// <summary>Fragen mit der zuletzt eingetragenen Tätigkeit (@LAST@).</summary>
        public Quip[] LastActivity { get; init; } = [];
        public Quip[] Sayings { get; init; } = [];
        /// <summary>Sprüche, die nur an bestimmten Tagen oder zu bestimmten Uhrzeiten erscheinen.</summary>
        public Quip[] Situational { get; init; } = [];
        public string[] Facts { get; init; } = [];
        public string[] FactIntros { get; init; } = [];
        public string[] FactFollowUps { get; init; } = [];
        /// <summary>Aufbau einer Wissensfrage: {0} Einleitung, {1} Fakt, {2} Anschlussfrage.</summary>
        public string FactFormat { get; init; } = "{0} {1} {2}";
        public Quip[] Titles { get; init; } = [];
        public Quip[] OkTexts { get; init; } = [];
    }

    static bool Workday(DateTime t) => t.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
    static bool Between(DateTime t, int fromHour, int fromMinute, int toHour, int toMinute) =>
        t.TimeOfDay >= new TimeSpan(fromHour, fromMinute, 0) && t.TimeOfDay < new TimeSpan(toHour, toMinute, 0);

    /// <summary>Wann ein situativer Spruch passt (Schlüssel in quips.xx.json unter "situational").</summary>
    public static readonly IReadOnlyDictionary<string, Func<DateTime, bool>> Conditions = new Dictionary<string, Func<DateTime, bool>>
    {
        ["mondayMorning"] = t => t.DayOfWeek == DayOfWeek.Monday && t.Hour < 12,
        ["tuesday"] = t => t.DayOfWeek == DayOfWeek.Tuesday,
        ["wednesday"] = t => t.DayOfWeek == DayOfWeek.Wednesday,
        ["thursday"] = t => t.DayOfWeek == DayOfWeek.Thursday,
        ["fridayMorning"] = t => t.DayOfWeek == DayOfWeek.Friday && t.Hour < 12,
        ["fridayAfternoon"] = t => t.DayOfWeek == DayOfWeek.Friday && t.Hour >= 12,
        ["weekend"] = t => !Workday(t),
        ["earlyBird"] = t => t.Hour is >= 5 and < 8,
        ["hungry"] = t => Between(t, 11, 15, 12, 30),
        ["lunch"] = t => Between(t, 12, 0, 13, 30),
        ["afternoonDip"] = t => Between(t, 14, 0, 16, 0),
        ["cake"] = t => Between(t, 15, 0, 16, 30),
        ["evening"] = t => t.Hour is >= 17 and < 21,
        ["night"] = t => t.Hour >= 21 || t.Hour < 5,
        ["monthEnd"] = t => t.Day > DateTime.DaysInMonth(t.Year, t.Month) - 3,
        ["monthStart"] = t => t.Day == 1,
        ["christmas"] = t => t.Month == 12 && t.Day <= 23,
        ["newYear"] = t => t.Month == 1 && t.Day <= 7,
    };

    static readonly Dictionary<string, Set> sets = [];

    /// <summary>Sprüche einer Sprache (fehlt die Datei: Englisch, sonst Deutsch).</summary>
    public static Set For(string code)
    {
        lock (sets)
        {
            if (sets.TryGetValue(code, out var set)) return set;
            var json = L.Resource($"quips.{code}.json");
            set = json is not null ? Parse(json) : code switch { "de" => new Set(), "en" => For("de"), _ => For("en") };
            return sets[code] = set;
        }
    }

    static Set Current => For(L.Current.Code);

    static Set Parse(string json)
    {
        var root = JsonDocument.Parse(json).RootElement;
        Quip[] List(string name) => root.TryGetProperty(name, out var a) ? a.EnumerateArray().Select(ToQuip).ToArray() : [];
        string[] Strings(string name) => root.TryGetProperty(name, out var a) ? a.EnumerateArray().Select(e => e.GetString()!).ToArray() : [];
        static Quip ToQuip(JsonElement e) => new(e[0].GetInt32(), e[1].GetString()!);

        var situational = new List<Quip>();
        if (root.TryGetProperty("situational", out var sit))
            foreach (var group in sit.EnumerateObject())
            {
                var when = Conditions.TryGetValue(group.Name, out var w)
                    ? w : throw new FormatException($"Unbekannte Bedingung in den Sprüchen: {group.Name}");
                situational.AddRange(group.Value.EnumerateArray().Select(e => ToQuip(e) with { When = when }));
            }

        return new Set
        {
            Questions = List("questions"),
            LastActivity = List("lastActivity"),
            Sayings = List("sayings"),
            Situational = situational.ToArray(),
            Facts = Strings("facts"),
            FactIntros = Strings("factIntros"),
            FactFollowUps = Strings("factFollowUps"),
            FactFormat = root.TryGetProperty("factFormat", out var f) ? f.GetString()! : "{0} {1} {2}",
            Titles = List("titles"),
            OkTexts = List("okTexts"),
        };
    }

    // Kurzzugriff auf die Sprüche der eingestellten Sprache.
    public static Quip[] Questions => Current.Questions;
    public static Quip[] LastActivity => Current.LastActivity;
    public static Quip[] Sayings => Current.Sayings;
    public static Quip[] Situational => Current.Situational;
    public static string[] FactIntros => Current.FactIntros;
    public static string[] FactFollowUps => Current.FactFollowUps;
    public static Quip[] Titles => Current.Titles;
    public static Quip[] OkTexts => Current.OkTexts;

    /// <summary>Anzeigebreite: chinesische, japanische und koreanische Zeichen sind etwa doppelt so breit.</summary>
    public static int Width(string text) => text.Sum(c => c >= '⺀' && c <= '￯' ? 2 : 1);

    public static IEnumerable<Quip> Candidates(DateTime now, string? lastActivity) => Candidates(Current, now, lastActivity);

    /// <summary>Alle Fragen, die zum Zeitpunkt <paramref name="now"/> möglich sind (Faktenfragen ohne Anschlussfrage).</summary>
    public static IEnumerable<Quip> Candidates(Set set, DateTime now, string? lastActivity)
    {
        var last = lastActivity?.Trim();
        if (last is { Length: > MaxLastLength }) last = last[..(MaxLastLength - 1)] + "…";
        foreach (var q in set.Questions) yield return q;
        foreach (var q in set.Sayings) yield return q;
        foreach (var q in set.Situational.Where(q => q.When!(now))) yield return q;
        if (!string.IsNullOrEmpty(last))
            foreach (var q in set.LastActivity) yield return q with { Text = q.Text.Replace("@LAST@", last) };
        foreach (var fact in set.Facts)
            yield return new Quip(FactWeight, fact, IsFact: true);
    }

    /// <summary>Alle möglichen Wissensfragen mit jeder Einleitung und Anschlussfrage (für die Längenprüfung).</summary>
    public static IEnumerable<string> AllFactTexts(Set set) =>
        from f in set.Facts from i in set.FactIntros from u in set.FactFollowUps select string.Format(set.FactFormat, i, f, u);

    public static string Question(DateTime now, string? lastActivity, Random random) => Question(Current, now, lastActivity, random);

    public static string Question(Set set, DateTime now, string? lastActivity, Random random)
    {
        var quip = Pick(Candidates(set, now, lastActivity).ToList(), random);
        return quip.IsFact
            ? string.Format(set.FactFormat, set.FactIntros[random.Next(set.FactIntros.Length)], quip.Text,
                set.FactFollowUps[random.Next(set.FactFollowUps.Length)])
            : quip.Text;
    }

    public static string Title(Random random) => Pick(Current.Titles, random).Text;
    public static string OkText(Random random) => Pick(Current.OkTexts, random).Text;

    static Quip Pick(IReadOnlyList<Quip> list, Random random)
    {
        int total = list.Sum(q => q.Weight);
        int r = random.Next(total);
        foreach (var q in list)
        {
            if (r < q.Weight) return q;
            r -= q.Weight;
        }
        return list[0];
    }
}
