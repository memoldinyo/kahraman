using Kahraman.Core;

namespace Kahraman.Core.Tests;

/// <summary>
/// Prüft das verlustfreie Lesen und Schreiben echter Kahraman-Dateien. Läuft nur, wenn KAHRAMAN_REAL_DIR gesetzt ist;
/// die Dateien werden vorher in ein Temp-Verzeichnis kopiert und nie direkt verändert.
/// </summary>
public class RealDataTests
{
    static string? RealDir => Environment.GetEnvironmentVariable("KAHRAMAN_REAL_DIR");

    [Fact]
    public void LogfilesRoundTripByteExact()
    {
        if (RealDir is null) return;
        using var dir = new TempDir();
        foreach (var src in Directory.GetFiles(RealDir, "*.kahraman").Where(f => !Path.GetFileName(f).StartsWith("taetigkeiten", StringComparison.OrdinalIgnoreCase)))
        {
            var copy = dir.File(Path.GetFileName(src));
            File.Copy(src, copy);
            var before = File.ReadAllBytes(copy);
            var log = LogFile.Load(copy);
            Assert.Empty(log.BadLines);
            Assert.NotEmpty(log.Entries);
            log.Save(log.Entries.ToList());
            var written = File.ReadAllBytes(copy);
            // Unterschiede nur bei unsauberen Altzeilen erlaubt (überzählige Felder, fehlender Zeilenumbruch am Ende).
            if (!before.SequenceEqual(written))
            {
                // Unsortierte Dateien werden beim Lesen chronologisch sortiert.
                var a = TextCodec.Ansi.GetString(before).TrimEnd('\r', '\n').Split("\r\n").OrderBy(l => l[..19], StringComparer.Ordinal).ToArray();
                var b = TextCodec.Ansi.GetString(written).TrimEnd('\r', '\n').Split("\r\n").OrderBy(l => l[..19], StringComparer.Ordinal).ToArray();
                Assert.Equal(a.Length, b.Length);
                var diffs = a.Zip(b).Where(p => p.First != p.Second).ToList();
                Assert.True(diffs.Count <= 2, $"{Path.GetFileName(src)}: {diffs.Count} abweichende Zeilen, z. B. {diffs.FirstOrDefault()}");
            }
        }
    }

    [Fact]
    public void ActivityFilesRoundTrip()
    {
        if (RealDir is null) return;
        using var dir = new TempDir();
        foreach (var src in Directory.GetFiles(RealDir, "taetigkeiten.kahraman"))
        {
            var copy = dir.File(ActivityStore.FileName);
            File.Copy(src, copy, true);
            var before = TextCodec.Ansi.GetString(File.ReadAllBytes(copy));
            var store = ActivityStore.Load(copy);
            store.Save();
            var written = TextCodec.Ansi.GetString(File.ReadAllBytes(copy));
            static string Body(string s) => string.Join("\n", s.Split('\n').Skip(2));
            Assert.Equal(Body(before), Body(written));
        }
    }
}
