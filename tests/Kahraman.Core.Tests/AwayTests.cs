namespace Kahraman.Core.Tests;

public class AwayTests
{
    static readonly DateTime Day = new(2026, 11, 4);
    static DateTime At(int h, int m, int dayOffset = 0) => Day.AddDays(dayOffset).AddHours(h).AddMinutes(m);
    static LogEntry Entry(DateTime t, string a = "Acme") => new(t, a, "", "");
    static readonly (string, string) Work = ("Acme", "Design");

    [Fact]
    public void LunchLockBooksWorkUntilLeavingAndThePause()
    {
        var away = new AwayState(At(12, 2), AwayKind.Lock);
        var entries = Away.Entries(away, Entry(At(11, 30)), At(12, 47), Work, PauseChoice.UntilBack, "Pause", "Start");
        Assert.Equal([new LogEntry(At(12, 2), "Acme", "Design", ""), new LogEntry(At(12, 47), "Pause", "", "")], entries);
    }

    [Fact]
    public void AwayWasWorkLeavesTheGapForTheNextEntry()
    {
        var away = new AwayState(At(14, 0), AwayKind.Idle);
        var entries = Away.Entries(away, Entry(At(13, 0)), At(15, 0), Work, PauseChoice.None, "Pause", "Start");
        Assert.Equal([new LogEntry(At(14, 0), "Acme", "Design", "")], entries);
    }

    [Fact]
    public void OvernightLogsTheEveningAndStartsTheNewDay()
    {
        var away = new AwayState(At(17, 2), AwayKind.Shutdown);
        var entries = Away.Entries(away, Entry(At(16, 30)), At(8, 1, 1), Work, PauseChoice.UntilBack, "Pause", "Start");
        Assert.Equal([new LogEntry(At(17, 2), "Acme", "Design", ""), new LogEntry(At(8, 1, 1), "Start", "", "")], entries);
    }

    [Fact]
    public void NoStartEntryIfTheNewDayAlreadyHasOne()
    {
        var away = new AwayState(At(17, 2), AwayKind.EndOfDay, WorkLogged: true);
        var entries = Away.Entries(away, Entry(At(7, 50, 1)), At(8, 1, 1), null, PauseChoice.None, "Pause", "Start");
        Assert.Empty(entries);
    }

    [Fact]
    public void BreakFromTheMenuDoesNotAskForWorkAgain()
    {
        var away = new AwayState(At(12, 0), AwayKind.Break, At(12, 30), WorkLogged: true);
        Assert.False(Away.NeedsWorkEntry(away, Entry(At(12, 0))));
        var entries = Away.Entries(away, Entry(At(12, 0)), At(12, 45), Work, PauseChoice.UntilPlanned, "Pause", "Start");
        Assert.Equal([new LogEntry(At(12, 30), "Pause", "", "")], entries);
    }

    [Fact]
    public void LateReturnFromBreakCanBookUntilNow()
    {
        var away = new AwayState(At(12, 0), AwayKind.Break, At(12, 30), WorkLogged: true);
        Assert.True(Away.PlannedEndPassed(away, At(12, 45)));
        var entries = Away.Entries(away, Entry(At(12, 0)), At(12, 45), null, PauseChoice.UntilBack, "Pause", "Start");
        Assert.Equal([new LogEntry(At(12, 45), "Pause", "", "")], entries);
    }

    [Fact]
    public void NoWorkEntryWhenEverythingWasLoggedOrTheLastEntryIsFromAnotherDay()
    {
        Assert.False(Away.NeedsWorkEntry(new AwayState(At(12, 0), AwayKind.Lock), Entry(At(12, 0))));
        Assert.False(Away.NeedsWorkEntry(new AwayState(At(9, 0), AwayKind.Lock), Entry(At(17, 0, -1))));
        Assert.False(Away.NeedsWorkEntry(new AwayState(At(9, 0), AwayKind.Lock), null));
        Assert.True(Away.NeedsWorkEntry(new AwayState(At(12, 0), AwayKind.Lock), Entry(At(11, 0))));
    }

    [Fact]
    public void ShortAbsencesAreIgnoredUnlessChosenInTheMenu()
    {
        var min = TimeSpan.FromMinutes(10);
        Assert.False(Away.Relevant(new AwayState(At(10, 0), AwayKind.Lock), At(10, 5), min));
        Assert.True(Away.Relevant(new AwayState(At(10, 0), AwayKind.Lock), At(10, 10), min));
        Assert.True(Away.Relevant(new AwayState(At(10, 0), AwayKind.Break), At(10, 1), min));
    }

    [Fact]
    public void EntriesNeverGoBeforeTheLastEntry()
    {
        // Während der "Abwesenheit" wurde doch noch etwas eingetragen (z. B. Leerlauf beim Lesen, dann Eintrag).
        var away = new AwayState(At(10, 0), AwayKind.Idle);
        var entries = Away.Entries(away, Entry(At(10, 30)), At(11, 0), Work, PauseChoice.UntilBack, "Pause", "Start");
        Assert.Equal([new LogEntry(At(11, 0), "Pause", "", "")], entries);
    }

    [Fact]
    public void StateSurvivesSavingAsJson()
    {
        var away = new AwayState(At(17, 2), AwayKind.Shutdown, At(18, 0), true);
        var json = System.Text.Json.JsonSerializer.Serialize(away);
        Assert.Contains("\"Shutdown\"", json);
        Assert.Equal(away, System.Text.Json.JsonSerializer.Deserialize<AwayState>(json));
    }
}
