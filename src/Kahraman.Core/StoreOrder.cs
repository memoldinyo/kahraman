namespace Kahraman.Core;

/// <summary>
/// Feste, vom Gebietsschema unabhängige Reihenfolge in der Tätigkeiten-Datei: Windows-1252-Bytes als
/// signed char verglichen, A–Z ohne Beachtung der Groß-/Kleinschreibung. So bleibt die Datei auf allen Rechnern
/// gleich sortiert, wenn sie im Netz gemeinsam genutzt wird.
/// </summary>
public sealed class StoreOrder : IComparer<string>
{
    public static readonly StoreOrder Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        int n = Math.Min(x.Length, y.Length);
        for (int i = 0; i < n; i++)
        {
            int c = Key(x[i]).CompareTo(Key(y[i]));
            if (c != 0) return c;
        }
        return x.Length.CompareTo(y.Length);
    }

    static int Key(char c)
    {
        if (c is >= 'A' and <= 'Z') return c + 32;
        if (c < 0x80) return c;
        var bytes = TextCodec.Ansi.GetBytes(c.ToString());
        if (bytes.Length == 1 && bytes[0] != (byte)'?') return (sbyte)bytes[0];
        return c; // Zeichen außerhalb von Windows-1252: hinter alles andere
    }
}
