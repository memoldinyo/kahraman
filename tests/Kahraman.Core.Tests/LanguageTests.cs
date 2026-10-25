using System.Text.RegularExpressions;
using Kahraman.Core;

namespace Kahraman.Core.Tests;

public partial class LanguageTests
{
    public static TheoryData<string> Languages => new(L.All.Select(l => l.Code));

    static readonly Dictionary<string, string> German = L.Texts("de");

    [GeneratedRegex(@"\{(\d+)(:[^}]*)?\}")] private static partial Regex Placeholder();
    [GeneratedRegex(@"\$(YES|NO|CANCEL)\$")] private static partial Regex ButtonToken();
    [GeneratedRegex(@"L\.[TF]\(\s*""([^""]+)""")] private static partial Regex KeyUse();

    static string Root([System.Runtime.CompilerServices.CallerFilePath] string file = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "..", ".."));

    static int Mnemonics(string s) => s.Replace("&&", "").Count(c => c == '&');

    [Theory, MemberData(nameof(Languages))]
    public void EveryLanguageHasExactlyTheGermanKeys(string code)
    {
        var texts = L.Texts(code);
        var missing = German.Keys.Except(texts.Keys).ToList();
        var extra = texts.Keys.Except(German.Keys).ToList();
        Assert.True(missing.Count == 0, $"{code}: fehlt " + string.Join(", ", missing));
        Assert.True(extra.Count == 0, $"{code}: überzählig " + string.Join(", ", extra));
        Assert.All(texts, t => Assert.False(string.IsNullOrWhiteSpace(t.Value), $"{code}: {t.Key} ist leer"));
    }

    [Theory, MemberData(nameof(Languages))]
    public void PlaceholdersButtonsAndShortcutsMatchGerman(string code)
    {
        foreach (var (key, translated) in L.Texts(code))
        {
            if (!German.TryGetValue(key, out var de)) continue;
            Assert.True(Placeholders(de).SetEquals(Placeholders(translated)), $"{code}: Platzhalter in {key}");
            Assert.True(Tokens(de).SetEquals(Tokens(translated)), $"{code}: $YES$/$NO$ in {key}");
            Assert.True(Mnemonics(de) == Mnemonics(translated), $"{code}: & (Tastenkürzel) in {key}: {translated}");
            _ = string.Format(translated, new object?[10]); // wirft bei kaputten Klammern
        }

        static HashSet<string> Placeholders(string s) => Placeholder().Matches(s).Select(m => m.Value).ToHashSet();
        static HashSet<string> Tokens(string s) => ButtonToken().Matches(s).Select(m => m.Value).ToHashSet();
    }

    [Theory, MemberData(nameof(Languages))]
    public void EveryLanguageHasAHelpText(string code)
    {
        var help = L.Resource($"help.{code}.txt");
        Assert.False(string.IsNullOrWhiteSpace(help), $"help.{code}.txt fehlt");
        Assert.Contains("taetigkeiten.kahraman", help);
    }

    [Fact]
    public void EveryKeyUsedInTheCodeExists()
    {
        var root = Root();
        var used = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => KeyUse().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToHashSet();
        Assert.NotEmpty(used);
        var missing = used.Where(k => !German.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, "Im Code benutzt, aber nicht in ui.de.json: " + string.Join(", ", missing));
    }

    [Fact]
    public void FallsBackToEnglishThenGermanThenKey()
    {
        try
        {
            L.Use("de");
            Assert.Equal("Abbrechen", L.T("button.cancel"));
            Assert.Equal("gibt.es.nicht", L.T("gibt.es.nicht"));
            L.Use("xx");
            Assert.Equal("en", L.Current.Code);
        }
        finally { L.Use("de"); }
    }
}
