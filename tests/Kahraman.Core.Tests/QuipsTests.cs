using Kahraman.Core;

namespace Kahraman.Core.Tests;

public class QuipsTests
{
    static readonly string LongActivity = new('X', 80);

    public static TheoryData<string> Languages => new(L.All.Select(l => l.Code));

    static IEnumerable<DateTime> EveryHalfHourOfAWeek()
    {
        var start = new DateTime(2026, 10, 26); // Montag
        for (int i = 0; i < 7 * 48; i++) yield return start.AddMinutes(30 * i);
    }

    [Theory, MemberData(nameof(Languages))]
    public void EveryLanguageHasAllKindsOfQuips(string code)
    {
        var set = Quips.For(code);
        Assert.Same(set, Quips.For(code));
        Assert.NotEmpty(set.Questions);
        Assert.NotEmpty(set.Sayings);
        Assert.NotEmpty(set.Situational);
        Assert.NotEmpty(set.Facts);
        Assert.NotEmpty(set.FactIntros);
        Assert.NotEmpty(set.FactFollowUps);
        Assert.NotEmpty(set.Titles);
        Assert.NotEmpty(set.OkTexts);
        Assert.All(set.LastActivity, q => Assert.Contains("@LAST@", q.Text));
        Assert.NotNull(L.Resource($"quips.{code}.json"));
    }

    [Theory, MemberData(nameof(Languages))]
    public void AllQuestionsFitIntoOneLine(string code)
    {
        var set = Quips.For(code);
        var texts = set.Questions.Concat(set.Sayings).Concat(set.Situational).Select(q => q.Text)
            .Concat(Quips.Candidates(set, DateTime.Now, LongActivity).Select(q => q.Text))
            .Concat(Quips.AllFactTexts(set));
        var tooLong = texts.Where(t => Quips.Width(t) > Quips.MaxQuestionLength).ToList();
        Assert.True(tooLong.Count == 0, "Zu lang: " + string.Join(" | ", tooLong));
    }

    [Theory, MemberData(nameof(Languages))]
    public void OkTextsAndTitlesAreShort(string code)
    {
        var set = Quips.For(code);
        Assert.All(set.OkTexts, q => Assert.True(Quips.Width(q.Text.Replace("&", "")) <= Quips.MaxOkLength, q.Text));
        Assert.All(set.OkTexts, q => Assert.Equal(1, q.Text.Count(c => c == '&')));
        Assert.All(set.Titles, q => Assert.True(Quips.Width(q.Text) <= 60, q.Text));
    }

    [Theory, MemberData(nameof(Languages))]
    public void PlainQuestionsAreNotTheMajority(string code)
    {
        var set = Quips.For(code);
        foreach (var t in EveryHalfHourOfAWeek())
        {
            var all = Quips.Candidates(set, t, "Projekt").ToList();
            var plain = set.Questions.Sum(q => q.Weight);
            Assert.InRange((double)plain / all.Sum(q => q.Weight), 0.15, 0.5);
        }
    }

    [Theory, MemberData(nameof(Languages))]
    public void FactsGetAFollowUpQuestion(string code)
    {
        var set = Quips.For(code);
        var random = new Random(1);
        var facts = Enumerable.Range(0, 2000)
            .Select(_ => Quips.Question(set, new DateTime(2026, 10, 21, 10, 0, 0), "Projekt", random))
            .Where(q => set.FactIntros.Any(q.StartsWith)).ToList();
        Assert.NotEmpty(facts);
        Assert.All(facts, f => Assert.Contains(set.FactFollowUps, f.EndsWith));
    }

    [Fact]
    public void SituationalSayingsDependOnDayAndTime()
    {
        var set = Quips.For("de");
        List<string> At(DateTime t) => Quips.Candidates(set, t, null).Select(q => q.Text).ToList();
        const string weekend = "Hey, bald ist Wochenende! Was steht noch an?";

        Assert.Contains(weekend, At(new DateTime(2026, 10, 23, 15, 0, 0)));        // Freitagnachmittag
        Assert.DoesNotContain(weekend, At(new DateTime(2026, 10, 26, 9, 0, 0)));   // Montagmorgen
        Assert.Contains(At(new DateTime(2026, 10, 26, 9, 0, 0)), t => t.StartsWith("Montag."));
        Assert.Contains(At(new DateTime(2026, 10, 25, 11, 0, 0)), t => t.StartsWith("Arbeiten am Wochenende"));
        Assert.Contains(At(new DateTime(2026, 10, 21, 12, 15, 0)), t => t.StartsWith("Mahlzeit"));
        Assert.Contains(At(new DateTime(2026, 2, 27, 10, 0, 0)), t => t.StartsWith("Monatsende"));
        Assert.DoesNotContain(At(new DateTime(2026, 2, 20, 10, 0, 0)), t => t.StartsWith("Monatsende"));
    }

    [Fact]
    public void LastActivityIsInsertedAndShortened()
    {
        var set = Quips.For("de");
        var texts = Quips.Candidates(set, DateTime.Now, LongActivity).Select(q => q.Text).ToList();
        Assert.Contains(texts, t => t.Contains(new string('X', 29) + "…"));
        Assert.DoesNotContain(Quips.Candidates(set, DateTime.Now, null), q => q.Text.Contains("@LAST@"));
    }

    [Fact]
    public void ChineseCharactersCountDouble()
    {
        Assert.Equal(5, Quips.Width("ab中c"));
        Assert.Equal(3, Quips.Width("abc"));
    }
}
