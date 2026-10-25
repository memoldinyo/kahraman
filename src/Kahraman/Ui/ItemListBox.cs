using System.Drawing.Drawing2D;

namespace Kahraman.Ui;

public sealed class ListItem(string text, int status, bool recent = false)
{
    public string Text { get; } = text;
    public int Status { get; } = status;
    /// <summary>Zuletzt benutzter Eintrag (oben, fett).</summary>
    public bool Recent { get; } = recent;
    /// <summary>Gestrichelte Linie unter dem Eintrag (Ende der zuletzt benutzten Einträge).</summary>
    public bool SeparatorBelow { get; set; }
    public override string ToString() => Text;
}

/// <summary>Auswahlliste mit farbiger Priorisierung, fett gedruckten "Top-Level-Einträgen" und F3-Suche.</summary>
public sealed class ItemListBox : ListBox
{
    Font? boldFont;

    public ItemListBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        IntegralHeight = false;
        HorizontalScrollbar = false;
        UpdateItemHeight();
    }

    void UpdateItemHeight()
    {
        boldFont?.Dispose();
        boldFont = new Font(Font, FontStyle.Bold);
        ItemHeight = Math.Max(Font.Height, boldFont.Height) + LogicalToDeviceUnits(4);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateItemHeight();
    }

    // Breite geändert (z. B. Kommentar eingeklappt): neu zeichnen, sonst bleiben alte "…"-Kürzungen stehen.
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) boldFont?.Dispose();
        base.Dispose(disposing);
    }

    public void SetItems(IEnumerable<ListItem> items)
    {
        BeginUpdate();
        Items.Clear();
        foreach (var i in items) Items.Add(i);
        EndUpdate();
    }

    public ListItem? SelectedListItem => SelectedItem as ListItem;

    /// <summary>Index des ersten Eintrags mit genau diesem Text (Groß-/Kleinschreibung und Leerzeichen am Rand egal).</summary>
    public int IndexOfText(string text)
    {
        var t = text.Trim();
        for (int i = 0; i < Items.Count; i++)
            if (string.Equals(((ListItem)Items[i]).Text.Trim(), t, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    /// <summary>Erster Eintrag, der mit dem Text beginnt – bevorzugt in der alphabetischen Liste, sonst oben bei den zuletzt benutzten.</summary>
    public int FindPrefix(string prefix)
    {
        var p = prefix.TrimStart();
        if (p.Length == 0) return -1;
        int recentHit = -1;
        for (int i = 0; i < Items.Count; i++)
        {
            var item = (ListItem)Items[i];
            if (!item.Text.TrimStart().StartsWith(p, StringComparison.CurrentCultureIgnoreCase)) continue;
            if (!item.Recent) return i;
            if (recentHit < 0) recentHit = i;
        }
        return recentHit;
    }

    // Suche beim Tippen in der Liste: "d", dann "a" springt zu "Da…" (nach kurzer Pause beginnt die Suche neu).
    string typed = "";
    DateTime lastTyped;

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar) && e.KeyChar != '\b')
        {
            base.OnKeyPress(e);
            return;
        }
        e.Handled = true;
        if ((DateTime.Now - lastTyped).TotalSeconds > 1.5) typed = "";
        lastTyped = DateTime.Now;
        typed = e.KeyChar == '\b' ? (typed.Length > 0 ? typed[..^1] : "") : typed + e.KeyChar;
        int idx = FindPrefix(typed);
        if (idx >= 0) SelectAndShow(idx);
    }

    /// <summary>Wählt den Eintrag und rückt ihn nach oben, damit ähnliche Einträge darunter sichtbar sind.</summary>
    public void SelectAndShow(int index)
    {
        SelectedIndex = index;
        TopIndex = Math.Max(0, index - 1);
    }

    /// <summary>Nächster Eintrag nach <paramref name="after"/>, der den Text enthält (mit Umlauf).</summary>
    public int FindSubstring(string fragment, int after)
    {
        if (fragment.Length == 0 || Items.Count == 0) return -1;
        for (int n = 1; n <= Items.Count; n++)
        {
            int i = (after + n + Items.Count) % Items.Count;
            var item = (ListItem)Items[i];
            if (!item.Recent && item.Text.Contains(fragment, StringComparison.CurrentCultureIgnoreCase)) return i;
        }
        return -1;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var item = (ListItem)Items[e.Index];
        var style = StatusStyle.Of(item.Status);
        bool selected = (e.State & DrawItemState.Selected) != 0;

        var p = Theme.Current;
        var g = e.Graphics;
        bool rtl = RightToLeft == RightToLeft.Yes;
        using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, e.Bounds);

        // Hinterlegung mit runden Ecken; ausgewählt: dezent in der Akzentfarbe mit Strich am Rand (wie Windows 11).
        var back = selected ? p.Selection : style.Back;
        var pill = new RectangleF(e.Bounds.X + 1, e.Bounds.Y + 1, e.Bounds.Width - 2, e.Bounds.Height - (item.SeparatorBelow ? 3 : 2));
        if (back is { } c)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Theme.Round(pill, LogicalToDeviceUnits(4));
            using var b = new SolidBrush(c);
            g.FillPath(b, path);
        }
        if (selected)
        {
            float w = LogicalToDeviceUnits(3), inset = pill.Height / 4;
            using var path = Theme.Round(new RectangleF(rtl ? pill.Right - w : pill.X, pill.Y + inset, w, pill.Height - 2 * inset), w / 2);
            using var b = new SolidBrush(p.Accent);
            g.FillPath(b, path);
        }

        var font = item.Recent || style.Bold ? boldFont! : Font;
        int pad = LogicalToDeviceUnits(8);
        var r = e.Bounds with { X = e.Bounds.X + pad, Width = e.Bounds.Width - 2 * pad };
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        // Arabisch: rechtsbündig und Lesereihenfolge von rechts.
        flags |= rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left;
        TextRenderer.DrawText(g, item.Text, font, r, style.Fore, flags);

        if (item.SeparatorBelow)
        {
            using var pen = new Pen(p.Muted) { DashStyle = DashStyle.Dash };
            g.DrawLine(pen, e.Bounds.Left + pad / 2, e.Bounds.Bottom - 1, e.Bounds.Right - pad / 2, e.Bounds.Bottom - 1);
        }
    }
}
