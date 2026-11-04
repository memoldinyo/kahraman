using System.Text.Json.Serialization;

namespace Kahraman.Core;

/// <summary>Warum Kahraman glaubt, dass niemand am Rechner ist.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AwayKind>))]
public enum AwayKind { Lock, SignOut, Shutdown, Sleep, Idle, Break, EndOfDay }

/// <summary>Seit wann jemand weg ist (wird in den Einstellungen gespeichert, damit es ein Herunterfahren übersteht).</summary>
/// <param name="Since">Zeitpunkt der letzten Eingabe vor dem Gehen.</param>
/// <param name="PlannedEnd">Bei einer Pause mit fester Dauer: geplantes Ende.</param>
/// <param name="WorkLogged">Die Arbeit bis <paramref name="Since"/> ist schon eingetragen (Pause/Feierabend über das Menü).</param>
public sealed record AwayState(DateTime Since, AwayKind Kind, DateTime? PlannedEnd = null, bool WorkLogged = false)
{
    /// <summary>Selbst gewählt (Menü) statt von Kahraman bemerkt.</summary>
    [JsonIgnore] public bool Manual => Kind is AwayKind.Break or AwayKind.EndOfDay;
}

/// <summary>Wie die Zeit einer Abwesenheit gebucht wird.</summary>
public enum PauseChoice
{
    /// <summary>Keine Pause buchen: die Zeit gehört zum nächsten Eintrag (z. B. Besprechung ohne Rechner).</summary>
    None,
    /// <summary>Pause vom Gehen bis zur Rückkehr.</summary>
    UntilBack,
    /// <summary>Pause bis zum geplanten Ende, danach wieder Arbeit.</summary>
    UntilPlanned,
}

/// <summary>Was Kahraman bei der Rückkehr fragt und welche Einträge daraus entstehen.</summary>
public static class Away
{
    /// <summary>Gab es seit dem letzten Eintrag Arbeit, die vor dem Gehen nicht mehr eingetragen wurde?
    /// Nur am selben Tag – der erste Eintrag eines Tages würde ohnehin nicht zählen.</summary>
    public static bool NeedsWorkEntry(AwayState away, LogEntry? last) =>
        !away.WorkLogged && last is not null && last.Time.Date == away.Since.Date && away.Since - last.Time >= TimeSpan.FromMinutes(1);

    /// <summary>Rückkehr an einem anderen Tag: kein Pausenblock, sondern Arbeitsbeginn.</summary>
    public static bool NewDay(AwayState away, DateTime back) => back.Date != away.Since.Date;

    /// <summary>Lohnt sich eine Rückfrage? Selbst gewählte Pausen immer, bemerkte Abwesenheiten erst ab der Mindestdauer.</summary>
    public static bool Relevant(AwayState away, DateTime back, TimeSpan minimum) =>
        away.Manual || back - away.Since >= minimum;

    /// <summary>Gibt es etwas zu fragen? Sonst (neuer Tag, alles eingetragen, Arbeitsbeginn steht schon) entfällt die Rückfrage.</summary>
    public static bool HasQuestion(AwayState away, LogEntry? last, DateTime back) =>
        NeedsWorkEntry(away, last) || !NewDay(away, back) || last?.Time.Date != back.Date;

    /// <summary>Das geplante Ende einer Pause liegt zwischen Gehen und Rückkehr (dann gibt es die Wahl "geplant" oder "jetzt").</summary>
    public static bool PlannedEndPassed(AwayState away, DateTime back) =>
        away.PlannedEnd is { } end && end > away.Since && end < back;

    /// <summary>
    /// Die Einträge, die nach der Rückfrage angehängt werden – in zeitlicher Reihenfolge und jeweils nach dem letzten Eintrag.
    /// </summary>
    /// <param name="work">Tätigkeit und Untertätigkeit bis zum Gehen; null = nicht eintragen.</param>
    public static List<LogEntry> Entries(AwayState away, LogEntry? last, DateTime back, (string Activity, string Sub)? work,
        PauseChoice pause, string breakActivity, string startActivity)
    {
        var result = new List<LogEntry>();
        var after = last?.Time ?? DateTime.MinValue;
        back = Seconds(back);

        void Add(DateTime time, string activity, string sub)
        {
            time = Seconds(time);
            if (time <= after || time > back) return;
            result.Add(new LogEntry(time, activity, sub, ""));
            after = time;
        }

        if (work is { } w && NeedsWorkEntry(away, last)) Add(away.Since, w.Activity, w.Sub);
        if (NewDay(away, back))
        {
            // Arbeitsbeginn, sofern heute noch nichts eingetragen ist.
            if (after.Date != back.Date) Add(back, startActivity, "");
        }
        else if (pause == PauseChoice.UntilBack)
        {
            Add(back, breakActivity, "");
        }
        else if (pause == PauseChoice.UntilPlanned && away.PlannedEnd is { } end)
        {
            Add(end < back ? end : back, breakActivity, "");
        }
        return result;
    }

    static DateTime Seconds(DateTime t) => t.AddTicks(-(t.Ticks % TimeSpan.TicksPerSecond));
}
