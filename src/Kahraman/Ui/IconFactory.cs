using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Kahraman.Ui;

/// <summary>Das kleine Zifferblatt im Infobereich der Taskleiste.</summary>
public static class IconFactory
{
    static Icon? appIcon;

    /// <summary>Programmsymbol aus der .exe (kahraman.ico, erzeugt aus IconVorlage.png mit testdata\make-icon.ps1).</summary>
    public static Icon AppIcon => appIcon ??= LoadAppIcon();

    static Icon LoadAppIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } exe && Icon.ExtractAssociatedIcon(exe) is { } icon) return icon;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException) { }
        return Clock(32, DateTime.Now);
    }

    /// <summary>Zeichnet ein Zifferblatt mit der angegebenen Uhrzeit.</summary>
    public static Icon Clock(int size, DateTime time, bool pending = false)
    {
        using var bmp = ClockBitmap(size, time.Hour % 12 + time.Minute / 60.0, time.Minute, pending);
        var hIcon = bmp.GetHicon();
        try { return (Icon)Icon.FromHandle(hIcon).Clone(); }
        finally { DestroyIcon(hIcon); }
    }

    static readonly Color SkyBlue = Color.FromArgb(125, 180, 230);
    static readonly Color Claret = Color.FromArgb(100, 25, 60);

    /// <param name="hour">Stellung des Stundenzeigers (0–12, Bruchteile erlaubt).</param>
    /// <param name="minute">Stellung des Minutenzeigers (0–60).</param>
    static Bitmap ClockBitmap(int size, double hour, double minute, bool pending)
    {
        var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float s = size;
            float pad = s * 0.06f;
            var face = new RectangleF(pad, pad, s - 2 * pad, s - 2 * pad);
            using (var fill = new SolidBrush(pending ? Color.FromArgb(255, 236, 160) : Color.White)) g.FillEllipse(fill, face);
            using (var rim = new Pen(SkyBlue, Math.Max(1.5f, s / 12f))) g.DrawEllipse(rim, face);

            var c = new PointF(s / 2, s / 2);
            float r = face.Width / 2;
            using (var tickPen = new Pen(SkyBlue, Math.Max(1f, s / 24f)))
                for (int i = 0; i < 12; i += 3)
                {
                    double a = i * Math.PI / 6;
                    g.DrawLine(tickPen, c.X + (float)Math.Sin(a) * r * 0.72f, c.Y - (float)Math.Cos(a) * r * 0.72f,
                        c.X + (float)Math.Sin(a) * r * 0.9f, c.Y - (float)Math.Cos(a) * r * 0.9f);
                }

            double hourAngle = hour * Math.PI / 6;
            double minuteAngle = minute * Math.PI / 30;
            using var hand = new Pen(Color.FromArgb(20, 20, 20), Math.Max(1.5f, s / 11f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(hand, c, new PointF(c.X + (float)Math.Sin(hourAngle) * r * 0.5f, c.Y - (float)Math.Cos(hourAngle) * r * 0.5f));
            using var minutePen = new Pen(Claret, Math.Max(1.2f, s / 15f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(minutePen, c, new PointF(c.X + (float)Math.Sin(minuteAngle) * r * 0.78f, c.Y - (float)Math.Cos(minuteAngle) * r * 0.78f));
        }
        return bmp;
    }

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);
}
