using Kahraman.Core;

namespace Kahraman.Core.Tests;

public class RecentCommentTests
{
    static LogEntry E(int minute, string act, string sub, string comment) =>
        new(new DateTime(2026, 10, 24, 8, minute, 0), act, sub, comment);

    [Fact]
    public void CommentOfLastEntryWithSameActivityAndSub()
    {
        var entries = new List<LogEntry>
        {
            E(0, "Acme 100001", "Lastenheft", "alt"),
            E(5, "Acme 100001", "Lastenheft", "Besprechung"),
            E(9, "Acme 100001", "Montage", ""),
            E(12, "Globex 100008", "Lastenheft", "anderes Projekt"),
        };
        Assert.Equal("Besprechung", Recent.Comment(entries, " acme 100001", "LASTENHEFT "));
        Assert.Equal("", Recent.Comment(entries, "Acme 100001", "Montage"));
        Assert.Equal("", Recent.Comment(entries, "Unbekannt", ""));
    }
}
