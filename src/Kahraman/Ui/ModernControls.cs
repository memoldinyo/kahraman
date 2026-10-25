using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Kahraman.Ui;

/// <summary>Flacher Knopf mit runden Ecken; der Standardknopf des Fensters ist in der Akzentfarbe gefüllt.</summary>
public class ModernButton : Button
{
    bool hover, pressed;

    public ModernButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
    }

    public override void NotifyDefault(bool value)
    {
        base.NotifyDefault(value);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = pressed = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed = e.Button == MouseButtons.Left; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? p.Back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        bool primary = IsDefault && Enabled;
        var fill = primary ? p.Accent : p.Surface;
        if (Enabled && pressed) fill = primary ? Theme.Mix(p.Accent, p.Text, 0.2f) : p.Line;
        else if (Enabled && hover) fill = primary ? Theme.Mix(p.Accent, p.Back, 0.15f) : p.Hover;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        float radius = LogicalToDeviceUnits(4);
        using (var path = Theme.Round(r, radius))
        {
            using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            using (var pen = new Pen(primary ? fill : p.Line)) g.DrawPath(pen, path);
        }
        if (Focused && ShowFocusCues)
        {
            var f = RectangleF.Inflate(r, -2.5f, -2.5f);
            using var path = Theme.Round(f, radius - 1);
            using var pen = new Pen(primary ? p.OnAccent : p.Accent);
            g.DrawPath(pen, path);
        }
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
        if (!ShowKeyboardCues) flags |= TextFormatFlags.HidePrefix;
        if (RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.RightToLeft;
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, !Enabled ? p.Muted : primary ? p.OnAccent : p.Text, flags);
    }
}

/// <summary>Kontrollkästchen mit rundem, in der Akzentfarbe gefülltem Häkchen.</summary>
public class ModernCheckBox : CheckBox
{
    bool hover;

    public ModernCheckBox() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

    public override Size GetPreferredSize(Size proposedSize) => Toggle.PreferredSize(this);

    protected override void OnPaint(PaintEventArgs e) =>
        Toggle.Paint(this, e.Graphics, Checked, hover, round: false, ShowKeyboardCues, ShowFocusCues);
}

/// <summary>Optionsfeld im selben Stil wie <see cref="ModernCheckBox"/>.</summary>
public class ModernRadioButton : RadioButton
{
    bool hover;

    public ModernRadioButton() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

    public override Size GetPreferredSize(Size proposedSize) => Toggle.PreferredSize(this);

    protected override void OnPaint(PaintEventArgs e) =>
        Toggle.Paint(this, e.Graphics, Checked, hover, round: true, ShowKeyboardCues, ShowFocusCues);
}

static class Toggle
{
    /// <summary>Feste Größe aus Kästchen und Text (unabhängig von der vorgeschlagenen Größe, sonst schaukelt sich das Layout auf).</summary>
    public static Size PreferredSize(ButtonBase c)
    {
        var text = TextRenderer.MeasureText(c.Text, c.Font, Size.Empty, TextFormatFlags.SingleLine);
        int box = c.LogicalToDeviceUnits(16), gap = c.LogicalToDeviceUnits(8);
        return new Size(box + gap + text.Width + c.Padding.Horizontal, Math.Max(text.Height, box) + c.LogicalToDeviceUnits(4) + c.Padding.Vertical);
    }

    /// <summary>Kästchen bzw. Kreis links (rechts bei Rechts-nach-links) und daneben der Text.</summary>
    public static void Paint(ButtonBase c, Graphics g, bool on, bool hover, bool round, bool keyboardCues, bool focusCues)
    {
        var p = Theme.Current;
        g.Clear(c.Parent?.BackColor ?? p.Back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        bool rtl = c.RightToLeft == RightToLeft.Yes;
        int box = c.LogicalToDeviceUnits(16), gap = c.LogicalToDeviceUnits(8);
        int y = (c.Height - box) / 2;
        var r = new RectangleF(rtl ? c.Width - box - 1 : 0.5f, y + 0.5f, box - 1, box - 1);
        using (var path = round ? Ellipse(r) : Theme.Round(r, c.LogicalToDeviceUnits(4)))
        {
            if (on && c.Enabled)
            {
                using var b = new SolidBrush(hover ? Theme.Mix(p.Accent, p.Back, 0.15f) : p.Accent);
                g.FillPath(b, path);
            }
            else
            {
                using (var b = new SolidBrush(p.Surface)) g.FillPath(b, path);
                using var pen = new Pen(!c.Enabled ? p.Line : hover ? p.Text : p.Muted);
                g.DrawPath(pen, path);
            }
        }
        if (on)
        {
            var mark = c.Enabled ? p.OnAccent : p.Muted;
            if (round)
            {
                float d = box * 0.4f;
                using var b = new SolidBrush(mark);
                g.FillEllipse(b, r.X + (r.Width - d) / 2, r.Y + (r.Height - d) / 2, d, d);
            }
            else
            {
                using var pen = new Pen(mark, Math.Max(1.5f, box / 9f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(pen, [
                    new PointF(r.Left + r.Width * 0.24f, r.Top + r.Height * 0.52f),
                    new PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.70f),
                    new PointF(r.Left + r.Width * 0.77f, r.Top + r.Height * 0.32f)]);
            }
        }
        var text = rtl ? new Rectangle(0, 0, c.Width - box - gap, c.Height) : new Rectangle(box + gap, 0, c.Width - box - gap, c.Height);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
        flags |= rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left;
        if (!keyboardCues) flags |= TextFormatFlags.HidePrefix;
        TextRenderer.DrawText(g, c.Text, c.Font, text, c.Enabled ? p.Text : p.Muted, flags);
        if (c.Focused && focusCues)
        {
            var size = TextRenderer.MeasureText(g, c.Text, c.Font, text.Size, flags);
            var f = new Rectangle(rtl ? text.Right - size.Width - 2 : text.Left - 2, (c.Height - size.Height) / 2, size.Width + 4, size.Height);
            using var pen = new Pen(p.Accent) { DashStyle = DashStyle.Dot };
            g.DrawRectangle(pen, f);
        }
    }

    static GraphicsPath Ellipse(RectangleF r)
    {
        var path = new GraphicsPath();
        path.AddEllipse(r);
        return path;
    }
}

/// <summary>Rahmen mit runden Ecken um ein Eingabefeld oder eine Liste; hat das Feld den Fokus, erscheint unten eine Akzentlinie.</summary>
public sealed class FieldFrame : Panel
{
    readonly Control inner;

    public FieldFrame(Control inner, Padding padding)
    {
        this.inner = inner;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        switch (inner)
        {
            case TextBoxBase t: t.BorderStyle = BorderStyle.None; break;
            case ListBox l: l.BorderStyle = BorderStyle.None; break;
        }
        inner.Dock = DockStyle.Fill;
        Padding = padding;
        Controls.Add(inner);
        Height = GetPreferredSize(Size.Empty).Height;
        Dock = DockStyle.Fill;
        inner.GotFocus += (_, _) => Invalidate();
        inner.LostFocus += (_, _) => Invalidate();
    }

    // Einzeiliges Feld: so hoch wie der Text; Listen und mehrzeilige Felder füllen ihre Zelle und brauchen keine Mindesthöhe.
    public override Size GetPreferredSize(Size proposedSize) =>
        new(proposedSize.Width, inner is TextBox { Multiline: false } t ? t.PreferredHeight + Padding.Vertical : Padding.Vertical + LogicalToDeviceUnits(20));

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        inner.Focus();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? p.Back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.Round(r, LogicalToDeviceUnits(5));
        using (var b = new SolidBrush(inner.BackColor)) g.FillPath(b, path);
        using (var pen = new Pen(p.Line)) g.DrawPath(pen, path);
        if (inner.ContainsFocus)
        {
            int h = LogicalToDeviceUnits(2);
            g.SetClip(path);
            using var b = new SolidBrush(p.Accent);
            g.FillRectangle(b, 0, Height - h - 1, Width, h + 1);
            g.ResetClip();
        }
    }
}

/// <summary>Registerkarten ohne eigene Reiter und ohne Rahmen; die Reiter zeichnet <see cref="PivotHeader"/>.</summary>
public sealed class HiddenTabControl : TabControl
{
    protected override void WndProc(ref Message m)
    {
        const int TCM_ADJUSTRECT = 0x1328;
        if (m.Msg == TCM_ADJUSTRECT && !DesignMode) { m.Result = 1; return; }
        base.WndProc(ref m);
    }
}

/// <summary>Reiter wie in den Windows-11-Einstellungen: Text, die aktive Seite mit Akzentlinie darunter.</summary>
public sealed class PivotHeader : Control
{
    readonly TabControl tabs;
    int hover = -1;

    public PivotHeader(TabControl tabs)
    {
        this.tabs = tabs;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        AccessibleRole = AccessibleRole.PageTabList;
        Dock = DockStyle.Top;
        tabs.SelectedIndexChanged += (_, _) => Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Height = Font.Height + LogicalToDeviceUnits(18);
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        Height = Font.Height + LogicalToDeviceUnits(18);
    }

    static string Plain(string s) => s.Replace("&", "");

    List<Rectangle> ItemRects(Graphics g)
    {
        var list = new List<Rectangle>();
        int x = LogicalToDeviceUnits(12), gap = LogicalToDeviceUnits(20);
        foreach (TabPage page in tabs.TabPages)
        {
            int w = TextRenderer.MeasureText(g, Plain(page.Text), Font).Width;
            list.Add(new Rectangle(x, 0, w, Height));
            x += w + gap;
        }
        if (RightToLeft == RightToLeft.Yes)
            for (int i = 0; i < list.Count; i++) list[i] = list[i] with { X = Width - list[i].Right };
        return list;
    }

    int HitTest(Point pt)
    {
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        var rects = ItemRects(g);
        int pad = LogicalToDeviceUnits(8);
        return rects.FindIndex(r => Rectangle.Inflate(r, pad, 0).Contains(pt));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = HitTest(e.Location);
        if (h != hover) { hover = h; Invalidate(); }
        Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int h = HitTest(e.Location);
        if (h >= 0) tabs.SelectedIndex = h;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? p.Back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var line = new Pen(p.Line)) g.DrawLine(line, 0, Height - 1, Width, Height - 1);
        var rects = ItemRects(g);
        int bar = LogicalToDeviceUnits(3);
        for (int i = 0; i < rects.Count; i++)
        {
            bool selected = i == tabs.SelectedIndex;
            var text = rects[i] with { Height = Height - bar };
            TextRenderer.DrawText(g, Plain(tabs.TabPages[i].Text), Font, text, selected || i == hover ? p.Text : p.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            if (!selected) continue;
            using var path = Theme.Round(new RectangleF(rects[i].X, Height - bar, rects[i].Width, bar), bar / 2f);
            using var b = new SolidBrush(p.Accent);
            g.FillPath(b, path);
        }
    }
}

/// <summary>Umschalter mit mehreren Feldern (Symbol und Text), z. B. System / Hell / Dunkel.</summary>
public sealed class SegmentedControl : Control
{
    readonly (string Glyph, string Text)[] items;
    int selected, hover = -1;

    public event EventHandler? SelectedIndexChanged;

    public SegmentedControl(params (string Glyph, string Text)[] items)
    {
        this.items = items;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        AutoSize = true; // Größe aus GetPreferredSize, damit das Layout (auch von rechts nach links) stimmt
        AccessibleRole = AccessibleRole.PageTabList;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => selected;
        set
        {
            value = Math.Clamp(value, 0, items.Length - 1);
            if (value == selected) return;
            selected = value;
            AccessibleName = items[value].Text;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    int Pad => LogicalToDeviceUnits(12);
    int IconGap => LogicalToDeviceUnits(6);

    int ItemWidth(Graphics g, int i) =>
        Pad * 2 + TextRenderer.MeasureText(g, items[i].Glyph, Theme.IconFont, Size.Empty, TextFormatFlags.NoPadding).Width + IconGap
        + TextRenderer.MeasureText(g, items[i].Text, Font, Size.Empty, TextFormatFlags.NoPadding).Width;

    public override Size GetPreferredSize(Size proposedSize)
    {
        using var g = Graphics.FromHwnd(IntPtr.Zero); // Bildschirm: misst ohne eigenes Fensterhandle
        int w = LogicalToDeviceUnits(4) + Enumerable.Range(0, items.Length).Sum(i => ItemWidth(g, i) + LogicalToDeviceUnits(2));
        return new Size(w, Font.Height + LogicalToDeviceUnits(14));
    }

    List<Rectangle> ItemRects(Graphics g)
    {
        var list = new List<Rectangle>();
        int x = LogicalToDeviceUnits(3), m = LogicalToDeviceUnits(3);
        for (int i = 0; i < items.Length; i++)
        {
            int w = ItemWidth(g, i);
            list.Add(new Rectangle(x, m, w, Height - 2 * m));
            x += w + LogicalToDeviceUnits(2);
        }
        if (RightToLeft == RightToLeft.Yes)
            for (int i = 0; i < list.Count; i++) list[i] = list[i] with { X = Width - list[i].Right };
        return list;
    }

    int HitTest(Point pt)
    {
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        return ItemRects(g).FindIndex(r => r.Contains(pt));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = HitTest(e.Location);
        if (h != hover) { hover = h; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        int h = HitTest(e.Location);
        if (h >= 0) SelectedIndex = h;
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int dir = e.KeyCode == Keys.Right ? 1 : e.KeyCode == Keys.Left ? -1 : 0;
        if (dir == 0) return;
        if (RightToLeft == RightToLeft.Yes) dir = -dir;
        SelectedIndex = selected + dir;
        e.Handled = true;
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? p.Back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var outer = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = Theme.Round(outer, LogicalToDeviceUnits(6)))
        {
            using (var b = new SolidBrush(p.Surface)) g.FillPath(b, path);
            using var pen = new Pen(Focused ? p.Accent : p.Line);
            g.DrawPath(pen, path);
        }
        var rects = ItemRects(g);
        for (int i = 0; i < items.Length; i++)
        {
            var r = rects[i];
            if (i == selected)
            {
                using var path = Theme.Round(r, LogicalToDeviceUnits(4));
                using var b = new SolidBrush(p.Selection);
                g.FillPath(b, path);
            }
            var color = i == selected || i == hover ? p.Text : p.Muted;
            var glyph = TextRenderer.MeasureText(g, items[i].Glyph, Theme.IconFont, Size.Empty, TextFormatFlags.NoPadding);
            var text = TextRenderer.MeasureText(g, items[i].Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            int total = glyph.Width + IconGap + text.Width;
            int x = r.X + (r.Width - total) / 2;
            bool rtl = RightToLeft == RightToLeft.Yes;
            var gr = new Rectangle(rtl ? x + text.Width + IconGap : x, r.Y, glyph.Width, r.Height);
            var tr = new Rectangle(rtl ? x : x + glyph.Width + IconGap, r.Y, text.Width + 2, r.Height);
            TextRenderer.DrawText(g, items[i].Glyph, Theme.IconFont, gr, color, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, items[i].Text, Font, tr, color, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }
}
