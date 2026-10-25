using System.Globalization;
using Kahraman.Core;

namespace Kahraman.Core.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kahraman-test-" + Guid.NewGuid().ToString("N"));
    public TempDir() => Directory.CreateDirectory(Path);
    public string File(string name) => System.IO.Path.Combine(Path, name);
    public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
}

public class LogFileTests
{
    static DateTime T(string s) => DateTime.ParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    [Fact]
    public void ParsesLogLine()
    {
        Assert.True(LogEntry.TryParse("2019-11-05 13:29:03\tSupport\tVertrieb\t\"\"", out var e));
        Assert.Equal(T("2019-11-05 13:29:03"), e!.Time);
        Assert.Equal("Support", e.Activity);
        Assert.Equal("Vertrieb", e.Sub);
        Assert.Equal("", e.Comment);
    }

    [Fact]
    public void ParsesLineWithSuperfluousField()
    {
        Assert.True(LogEntry.TryParse("2019-01-22 14:50:41\tKunde Beispiel 100006 \t\t\t\"\"", out var e));
        Assert.Equal("Kunde Beispiel 100006 ", e!.Activity);
        Assert.Equal("", e.Sub);
        Assert.Equal("", e.Comment);
    }

    [Fact]
    public void RoundTripsBytesExactly()
    {
        using var dir = new TempDir();
        var path = dir.File("user.kahraman");
        var ansi = TextCodec.Ansi;
        var expected = ansi.GetBytes("2019-01-22 12:18:30\tMüller & Söhne 4711\t\t\"\"\r\n2019-01-22 14:20:25\tX\tY\t\"Kommentar mit \"Zitat\"\"\r\n");
        File.WriteAllBytes(path, expected);
        var log = LogFile.Load(path);
        Assert.Equal(2, log.Entries.Count);
        Assert.Equal("Müller & Söhne 4711", log.Entries[0].Activity);
        Assert.Equal("Kommentar mit \"Zitat\"", log.Entries[1].Comment);
        log.Save(log.Entries.ToList());
        Assert.Equal(expected, File.ReadAllBytes(path));
    }

    [Fact]
    public void AppendAddsMissingNewlineAndRejectsEarlierTimes()
    {
        using var dir = new TempDir();
        var path = dir.File("user.kahraman");
        File.WriteAllText(path, "2020-01-01 08:00:00\tA\t\t\"\"");
        var log = LogFile.Load(path);
        log.Append(new LogEntry(T("2020-01-01 09:00:00"), "B", "b", "k"));
        Assert.Equal("2020-01-01 08:00:00\tA\t\t\"\"\r\n2020-01-01 09:00:00\tB\tb\t\"k\"\r\n", File.ReadAllText(path));
        Assert.Throws<KahramanException>(() => log.Append(new LogEntry(T("2020-01-01 09:00:00"), "C", "", "")));
        Assert.Throws<KahramanException>(() => log.Append(new LogEntry(T("2020-01-01 08:30:00"), "C", "", "")));
    }

    [Fact]
    public void NonAnsiCharactersSurviveAsUtf8Line()
    {
        using var dir = new TempDir();
        var path = dir.File("user.kahraman");
        var log = LogFile.Load(path);
        log.Append(new LogEntry(T("2020-01-01 08:00:00"), "Müller", "", ""));
        log.Append(new LogEntry(T("2020-01-01 09:00:00"), "Kayışdağı İş", "Ğ", "ş"));
        var reloaded = LogFile.Load(path);
        Assert.Equal("Müller", reloaded.Entries[0].Activity);
        Assert.Equal("Kayışdağı İş", reloaded.Entries[1].Activity);
        Assert.Equal("ş", reloaded.Entries[1].Comment);
    }

    [Fact]
    public void SaveRefusesFileWithUnreadableLines()
    {
        using var dir = new TempDir();
        var path = dir.File("user.kahraman");
        File.WriteAllText(path, "Kaputt\r\n2020-01-01 08:00:00\tA\t\t\"\"\r\n");
        var log = LogFile.Load(path);
        Assert.Single(log.BadLines);
        Assert.Throws<KahramanException>(() => log.Save(log.Entries));
    }

    [Fact]
    public void MergeRejectsSimultaneousEntries()
    {
        var a = new List<LogEntry> { new(T("2020-01-01 08:00:00"), "A", "", ""), new(T("2020-01-01 10:00:00"), "A", "", "") };
        var b = new List<LogEntry> { new(T("2020-01-01 09:00:00"), "B", "", ""), new(T("2020-01-01 10:00:00"), "B", "", "") };
        var merged = LogFile.Merge(a, b, out var conflicts);
        Assert.Single(conflicts);
        Assert.Equal(["A", "B", "A"], merged.Select(e => e.Activity));
    }
}

public class ActivityStoreTests
{
    const string Sample = """
        <?xml version="1.0" encoding="ISO-8859-1"?>
        <kahraman version="1" lastchangetimestamp="15.10.2026 12:44:55" lastchangeuser="u" lastchangehost="H">
          <taetigkeiten>
            <defaults/>
            <taetigkeit text="Acme 100001" status="0"/>
            <taetigkeit text="Alpha 100002" status="0">
              <taetigkeiten>
                <taetigkeit text="Netzwerk" status="0"/>
              </taetigkeiten>
            </taetigkeit>
            <taetigkeit text="Panel für 100003" status="4"/>
            <taetigkeit text="Umbau 100004 --&gt;8h" status="0"/>
            <taetigkeit text="Zeta 100005" status="0">
              <taetigkeiten>
                <taetigkeit text="Support Kunde" status="0"/>
                <taetigkeit text="Wartung &amp; Software Installation" status="1"/>
              </taetigkeiten>
            </taetigkeit>
          </taetigkeiten>
        </kahraman>

        """;

    static string Body(string xml) => string.Join("\n", xml.Replace("\r\n", "\n").Split('\n').Skip(2));

    [Fact]
    public void RoundTripsStoreFormat()
    {
        using var dir = new TempDir();
        var path = dir.File(ActivityStore.FileName);
        var input = Sample.Replace("\r\n", "\n");
        File.WriteAllBytes(path, TextCodec.Ansi.GetBytes(input));
        var store = ActivityStore.Load(path);
        Assert.Equal(5, store.Activities.Count);
        Assert.Equal(4, store.Find("panel FÜR 100003")!.Status);
        Assert.Equal("Wartung & Software Installation", store.Find("Zeta 100005")!.FindSub("wartung & software installation")!.Text);
        store.Save();
        var output = TextCodec.Ansi.GetString(File.ReadAllBytes(path));
        Assert.Equal(Body(input), Body(output));
    }

    [Fact]
    public void KeepsCaseInsensitiveOrderAndDefaults()
    {
        using var dir = new TempDir();
        var store = ActivityStore.CreateNew(dir.File(ActivityStore.FileName));
        store.Add("beta");
        store.Add("Alpha");
        store.Add("ALPHA"); // Duplikat (Groß-/Kleinschreibung egal)
        store.Defaults.Add(new Item("Doku"));
        store.Defaults.Add(new Item("Test"));
        Assert.Equal(2, store.InsertDefaults("alpha"));
        Assert.Equal(0, store.InsertDefaults("alpha"));
        store.Save();
        var reloaded = ActivityStore.Load(store.Path);
        Assert.Equal(["Alpha", "beta", ActivityStore.FirstActivity], reloaded.Activities.Values.Select(a => a.Text));
        Assert.Equal(["Doku", "Test"], reloaded.Defaults.Select(d => d.Text));
        Assert.Equal(2, reloaded.Find("Alpha")!.Subs.Count);
    }

    [Fact]
    public void FindIgnoresTrailingSpaces()
    {
        using var dir = new TempDir();
        var store = ActivityStore.CreateNew(dir.File(ActivityStore.FileName));
        store.Add("Beispiel 100007 ");
        Assert.Equal("Beispiel 100007 ", store.Find("beispiel 100007")!.Text);
    }

    [Fact]
    public void EscapesCharactersOutsideLatin1()
    {
        using var dir = new TempDir();
        var store = ActivityStore.CreateNew(dir.File(ActivityStore.FileName));
        store.Add("Kosten 5 € – Kayışdağı 😀");
        store.Save();
        var raw = File.ReadAllText(store.Path, TextCodec.Ansi);
        Assert.Contains("&#8364;", raw);
        Assert.Equal("Kosten 5 € – Kayışdağı 😀", ActivityStore.Load(store.Path).Find("Kosten 5 € – Kayışdağı 😀")!.Text);
    }
}

public class EvaluationTests
{
    static LogEntry E(string t, string a, string s = "", string c = "") =>
        new(DateTime.ParseExact(t, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), a, s, c);

    static readonly List<LogEntry> Log =
    [
        E("2024-01-08 08:00", "Timbuktu"),        // erster Eintrag des Tages: nur Startzeit
        E("2024-01-08 09:00", "Projekt", "Doku"),  // 1 h
        E("2024-01-08 10:30", "Projekt", "Code"),  // 1,5 h
        E("2024-01-08 11:00", "privat"),           // 0,5 h
        E("2024-01-08 23:30", "Projekt", "Code"),  // 12,5 h
        E("2024-01-09 00:30", "Nachts"),           // neuer Tag: zählt nicht
        E("2024-01-09 10:00", "projekt ", "code", "Fix"), // 9,5 h
    ];

    [Fact]
    public void FirstEntryOfDayOnlyMarksStart()
    {
        var spans = Evaluation.Spans(Log);
        Assert.DoesNotContain(spans, s => s.Entry.Activity is "Timbuktu" or "Nachts");
        Assert.Equal(25.0, spans.Sum(s => s.Duration.TotalHours));
    }

    [Fact]
    public void AggregatesByLevelCaseInsensitive()
    {
        var spans = Evaluation.Spans(Log);
        var week = Period.Of(PeriodKind.Woche, new DateOnly(2024, 1, 10));
        var none = Evaluation.ExcludedSet([]);
        var rows = Evaluation.Aggregate(spans, week, DetailLevel.Taetigkeiten, none);
        Assert.Equal(2, rows.Count);
        Assert.Equal(24.5, rows.Single(r => r.Activity == "Projekt").Hours, 6);
        Assert.Equal(24.5 / 8, rows.Single(r => r.Activity == "Projekt").Days, 6);

        var subs = Evaluation.Aggregate(spans, week, DetailLevel.Untertaetigkeiten, none);
        Assert.Equal(23.5, subs.Single(r => r.Sub == "Code").Hours, 6);

        var excluded = Evaluation.Aggregate(spans, week, DetailLevel.Taetigkeiten, Evaluation.ExcludedSet(["PRIVAT"]));
        Assert.Single(excluded);
    }

    [Fact]
    public void DetailsPerDay()
    {
        var spans = Evaluation.Spans(Log);
        var rows = Evaluation.Details(spans, new Period(new(2024, 1, 1), new(2024, 1, 31)), Evaluation.ExcludedSet([]), ["projekt"]);
        Assert.Equal(3, rows.Count);
        Assert.Equal(new DateOnly(2024, 1, 9), rows[^1].Day);
        Assert.Equal(9.5, rows[^1].Hours, 6);
        var text = Evaluation.DetailsAsText(rows, CultureInfo.GetCultureInfo("de-DE"));
        Assert.Contains("2024-01-08\t1,00\tProjekt\tDoku\t", text);
    }

    [Fact]
    public void Periods()
    {
        var d = new DateOnly(2026, 10, 23); // Freitag
        Assert.Equal(new Period(new(2026, 10, 19), new(2026, 10, 25)), Period.Of(PeriodKind.Woche, d));
        Assert.Equal(new Period(new(2026, 10, 1), new(2026, 12, 31)), Period.Of(PeriodKind.Quartal, d));
        var q = Period.Of(PeriodKind.Quartal, d).Shift(PeriodKind.Quartal, -1);
        Assert.Equal(new Period(new(2026, 7, 1), new(2026, 9, 30)), q);
        var m = Period.Of(PeriodKind.Monat, new DateOnly(2026, 1, 31)).Shift(PeriodKind.Monat, 1);
        Assert.Equal(new Period(new(2026, 2, 1), new(2026, 2, 28)), m);
        var free = new Period(new(2026, 1, 1), new(2026, 1, 10)).Shift(PeriodKind.Frei, 1);
        Assert.Equal(new Period(new(2026, 1, 11), new(2026, 1, 20)), free);
    }
}

public class StoreFileTests
{
    [Fact]
    public void RejectsForeignXml()
    {
        using var dir = new TempDir();
        var path = DataFiles.StorePath(dir.Path);
        File.WriteAllText(path, "<?xml version=\"1.0\"?>\n<anderes><taetigkeiten/></anderes>\n");
        Assert.Throws<KahramanException>(() => ActivityStore.Load(path));
    }
}

public class HelperTests
{
    [Fact]
    public void RecentListsAreDistinctNewestFirst()
    {
        var t = new DateTime(2024, 1, 1, 8, 0, 0);
        var log = new List<LogEntry>
        {
            new(t, "A", "a1", ""), new(t.AddHours(1), "B", "", ""), new(t.AddHours(2), "a", "a2", ""), new(t.AddHours(3), "C", "", ""),
        };
        Assert.Equal(["C", "a", "B"], Recent.Activities(log, 3));
        Assert.Equal(["a2", "a1"], Recent.Subs(log, "A", 5));
        Assert.Empty(Recent.Activities(log, 0));
    }

    [Fact]
    public void SubImportFindsActivityByPrefix()
    {
        using var dir = new TempDir();
        var store = ActivityStore.CreateNew(dir.File(ActivityStore.FileName));
        store.Add("Projekt Alpha 4711");
        store.Add("Projekt Beta");
        var (name, subs) = SubImport.Parse("Projekt Alpha\r\n\r\nPlanung\r\nUmsetzung\r\nplanung\r\n");
        Assert.Equal(["Planung", "Umsetzung"], subs);
        Assert.Equal("Projekt Alpha 4711", Assert.Single(SubImport.Candidates(store, name)).Text);
        Assert.Equal(2, SubImport.Candidates(store, "Projekt").Count);
    }
}
