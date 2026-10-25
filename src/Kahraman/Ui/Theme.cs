using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Kahraman.Ui;

/// <summary>Farben der Oberfläche.</summary>
public sealed record Palette(
    bool Dark, Color Back, Color Surface, Color Line, Color Text, Color Muted,
    Color Accent, Color OnAccent, Color Selection, Color Hover, Color Claret);

/// <summary>Hell- und Dunkelmodus: Farben, Schrift und Fensterrahmen für alle Fenster.</summary>
public static class Theme
{
    // Hellblau und Bordeaux wie im Programmsymbol; im hellen Modus etwas kräftiger für genug Kontrast.
    public static readonly Palette Light = new(false,
        Back: Hex(0xF3F3F3), Surface: Color.White, Line: Hex(0xD9D9D9), Text: Hex(0x1B1B1B), Muted: Hex(0x6B6B6B),
        Accent: Hex(0x2F76B8), OnAccent: Color.White, Selection: Hex(0xDCEBF8), Hover: Hex(0xF0F0F0), Claret: Hex(0x7A1F3D));

    public static readonly Palette Dark = new(true,
        Back: Hex(0x202020), Surface: Hex(0x2B2B2B), Line: Hex(0x3D3D3D), Text: Hex(0xF0F0F0), Muted: Hex(0x9A9A9A),
        Accent: Hex(0x7DB4E6), OnAccent: Hex(0x0F2740), Selection: Hex(0x3D4954), Hover: Hex(0x353535), Claret: Hex(0xC7738F));

    public static ThemeMode Mode { get; private set; }
    public static Palette Current { get; private set; } = Light;

    static Color Hex(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

    /// <summary>Stellt den Modus ein. Gilt für alle Fenster, die danach geöffnet werden.</summary>
    public static void Use(ThemeMode mode)
    {
        Mode = mode;
        Current = mode == ThemeMode.Dark || (mode == ThemeMode.System && WindowsAppsAreDark()) ? Dark : Light;
#pragma warning disable WFO5001 // Dunkelmodus von Windows Forms gilt noch als Vorschau
        Application.SetColorMode(Current.Dark ? SystemColorMode.Dark : SystemColorMode.Classic);
#pragma warning restore WFO5001
        ToolStripManager.Renderer = new MenuRenderer();
    }

    /// <summary>Windows-Einstellung geändert: im Modus "System" mitziehen. Liefert true, wenn sich etwas geändert hat.</summary>
    public static bool Refresh()
    {
        var before = Current;
        if (Mode == ThemeMode.System) Use(Mode);
        return Current != before;
    }

    static bool WindowsAppsAreDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
    }

    static Font? font, iconFont;

    /// <summary>Schrift von Windows 11 (Segoe UI Variable), sonst die normale Windows-Schrift.</summary>
    public static Font Font => font ??= Installed("Segoe UI Variable Text", 9f) ?? SystemFonts.MessageBoxFont!;

    /// <summary>Symbolschrift von Windows (Segoe Fluent Icons unter Windows 11, sonst Segoe MDL2 Assets).</summary>
    public static Font IconFont => iconFont ??= Installed("Segoe Fluent Icons", 10f) ?? Installed("Segoe MDL2 Assets", 10f) ?? Font;

    static Font? Installed(string family, float size)
    {
        var f = new Font(family, size);
        if (f.Name == family) return f;
        f.Dispose();
        return null;
    }

    /// <summary>Farben und Schrift auf ein Fenster anwenden; am Ende des Konstruktors aufrufen.</summary>
    public static void Apply(Form form)
    {
        var p = Current;
        form.BackColor = p.Back;
        form.ForeColor = p.Text;
        Style(form, p);
        if (form.IsHandleCreated) Frame(form);
        else form.HandleCreated += (_, _) => Frame(form);
    }

    static void Style(Control parent, Palette p)
    {
        foreach (Control c in parent.Controls)
        {
            switch (c)
            {
                case TextBoxBase or ListBox or ListView or ComboBox or NumericUpDown:
                    c.BackColor = p.Surface;
                    c.ForeColor = p.Text;
                    break;
                case TabPage page:
                    page.UseVisualStyleBackColor = false;
                    page.BackColor = p.Back;
                    break;
                case TrackBar:
                    c.BackColor = p.Back;
                    break;
            }
            Style(c, p);
        }
    }

    /// <summary>Titelleiste dunkel bzw. hell, Ecken rund (Windows 11).</summary>
    static void Frame(Form form)
    {
        int dark = Current.Dark ? 1 : 0, round = DWMWCP_ROUND;
        DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        DwmSetWindowAttribute(form.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
    }

    internal static void RoundCorners(IntPtr handle, bool small)
    {
        int round = small ? DWMWCP_ROUNDSMALL : DWMWCP_ROUND;
        DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
    }

    /// <summary>Windows 11 zeichnet runde Ecken und den Rand von Menüs selbst.</summary>
    internal static bool RoundedWindows => Environment.OSVersion.Version.Build >= 22000;

    const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20, DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2, DWMWCP_ROUNDSMALL = 3;
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { path.AddRectangle(r); return path; }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>Menüs (Infobereich, Kontextmenüs) in den Farben des Modus, mit runder Markierung.</summary>
sealed class MenuRenderer : ToolStripProfessionalRenderer
{
    public MenuRenderer() : base(new Colors()) => RoundedEdges = true;

    sealed class Colors : ProfessionalColorTable
    {
        static Palette P => Theme.Current;
        public override Color ToolStripDropDownBackground => P.Surface;
        public override Color ImageMarginGradientBegin => P.Surface;
        public override Color ImageMarginGradientMiddle => P.Surface;
        public override Color ImageMarginGradientEnd => P.Surface;
        public override Color MenuBorder => P.Line;
        public override Color MenuItemBorder => P.Selection;
        public override Color MenuItemSelected => P.Selection;
        public override Color MenuItemSelectedGradientBegin => P.Selection;
        public override Color MenuItemSelectedGradientEnd => P.Selection;
        public override Color SeparatorDark => P.Line;
        public override Color SeparatorLight => P.Surface;
        public override Color CheckBackground => P.Selection;
        public override Color CheckSelectedBackground => P.Selection;
        public override Color CheckPressedBackground => P.Selection;
    }

    protected override void Initialize(ToolStrip toolStrip)
    {
        base.Initialize(toolStrip);
        if (toolStrip is ToolStripDropDown && Theme.RoundedWindows)
            toolStrip.HandleCreated += (_, _) => Theme.RoundCorners(toolStrip.Handle, small: true);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is ToolStripDropDown && Theme.RoundedWindows) return;
        base.OnRenderToolStripBorder(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int inset = e.ToolStrip?.LogicalToDeviceUnits(4) ?? 4;
        var r = new RectangleF(inset, 1, e.Item.Width - 2 * inset, e.Item.Height - 2);
        using var path = Theme.Round(r, inset);
        using var b = new SolidBrush(Theme.Current.Selection);
        g.FillPath(b, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        var p = Theme.Current;
        // Eigene Farbe (z. B. Priorisierungen) behalten, sonst die Textfarbe des Modus.
        e.TextColor = !e.Item.Enabled ? p.Muted : e.Item.ForeColor == e.ToolStrip?.ForeColor ? p.Text : e.Item.ForeColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.Current.Text;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = e.ImageRectangle;
        r.Inflate(2, 2);
        using (var path = Theme.Round(r, 3))
        using (var b = new SolidBrush(Theme.Current.Selection))
            g.FillPath(b, path);
        using var pen = new Pen(Theme.Current.Accent, Math.Max(1.5f, r.Height / 9f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(pen, [
            new PointF(r.Left + r.Width * 0.26f, r.Top + r.Height * 0.52f),
            new PointF(r.Left + r.Width * 0.43f, r.Top + r.Height * 0.69f),
            new PointF(r.Left + r.Width * 0.75f, r.Top + r.Height * 0.33f)]);
    }
}
