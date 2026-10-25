using System.Drawing.Drawing2D;
using Kahraman.Core;

namespace Kahraman.Ui;

/// <summary>Schmale Leiste im Eingabefenster: ein Klick klappt das Feld links daneben ein oder aus (Kommentar, Untertätigkeiten).</summary>
public sealed class CommentStrip : Control
{
    bool expanded = true, hasText, hover;
    readonly string collapseKey, expandKey;

    public event EventHandler? Toggled;

    public CommentStrip(string collapseKey = "strip.collapse", string expandKey = "strip.expand")
    {
        this.collapseKey = collapseKey;
        this.expandKey = expandKey;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.Selectable, false);
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
    }

    /// <summary>Kommentarfeld sichtbar.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Expanded
    {
        get => expanded;
        set { expanded = value; AccessibleName = L.T(value ? collapseKey : expandKey); Invalidate(); }
    }

    /// <summary>Im (eingeklappten) Kommentarfeld steht etwas: kleiner Punkt unter dem Symbol.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool HasText
    {
        get => hasText;
        set { if (hasText != value) { hasText = value; Invalidate(); } }
    }

    public void Toggle()
    {
        Expanded = !Expanded;
        Toggled?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Toggle();
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

    protected override AccessibleObject CreateAccessibilityInstance() => new StripAccessible(this);

    sealed class StripAccessible(CommentStrip owner) : ControlAccessibleObject(owner)
    {
        public override string DefaultAction => L.T("strip.toggle");
        public override void DoDefaultAction() => owner.Toggle();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? p.Back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), LogicalToDeviceUnits(5)))
        {
            using (var b = new SolidBrush(hover ? p.Hover : p.Surface)) g.FillPath(b, path);
            using var border = new Pen(p.Line);
            g.DrawPath(border, path);
        }

        // Symbol: Fenster mit Seitenleiste und Pfeil (‹ = ausklappen, › = einklappen).
        var ink = hover ? p.Text : p.Muted;
        int w = LogicalToDeviceUnits(14), h = LogicalToDeviceUnits(11);
        var r = new Rectangle((Width - w) / 2, LogicalToDeviceUnits(8), w, h);
        using var pen = new Pen(ink, Math.Max(1f, LogicalToDeviceUnits(1)));
        using (var frame = Theme.Round(r, LogicalToDeviceUnits(2))) g.DrawPath(pen, frame);
        int bar = r.Right - w / 3;
        g.DrawLine(pen, bar, r.Top, bar, r.Bottom);
        float cx = r.Left + (bar - r.Left) / 2f, cy = r.Top + h / 2f, s = h / 4.5f;
        PointF[] arrow = expanded
            ? [new(cx - s / 2, cy - s), new(cx + s / 2, cy), new(cx - s / 2, cy + s)]
            : [new(cx + s / 2, cy - s), new(cx - s / 2, cy), new(cx + s / 2, cy + s)];
        using (var fill = new SolidBrush(ink)) g.FillPolygon(fill, arrow);

        if (!expanded && hasText)
        {
            int d = LogicalToDeviceUnits(5);
            using var dot = new SolidBrush(p.Accent);
            g.FillEllipse(dot, (Width - d) / 2, r.Bottom + LogicalToDeviceUnits(6), d, d);
        }
    }
}
