using System.Globalization;

namespace Kahraman.Core;

/// <summary>Eine Zeile im Logfile: Zeitpunkt, Tätigkeit, Untertätigkeit, Kommentar.</summary>
/// <remarks>Der Eintrag beschreibt die Zeit seit dem vorherigen Eintrag desselben Tages.</remarks>
public sealed record LogEntry(DateTime Time, string Activity, string Sub, string Comment)
{
    public const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    public string ToLine() =>
        $"{Time.ToString(TimeFormat, CultureInfo.InvariantCulture)}\t{Clean(Activity)}\t{Clean(Sub)}\t\"{Clean(Comment)}\"";

    public static string Clean(string s) =>
        s.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');

    public static bool TryParse(string line, out LogEntry? entry)
    {
        entry = null;
        var f = line.Split('\t');
        if (f.Length < 2) return false;
        if (!DateTime.TryParseExact(f[0], TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            return false;
        string activity = f[1];
        string sub = f.Length > 2 ? f[2] : "";
        // Der Kommentar steht immer im letzten Feld (vereinzelt gibt es überzählige leere Felder).
        string comment = f.Length > 3 ? f[^1] : "";
        if (comment.Length >= 2 && comment[0] == '"' && comment[^1] == '"')
            comment = comment[1..^1];
        entry = new LogEntry(time, activity, sub, comment);
        return true;
    }
}
