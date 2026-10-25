using System.Runtime.InteropServices;
using Kahraman.Core;

namespace Kahraman.Ui;

/// <summary>Meldungen mit dem Titel "Kahraman", von rechts nach links im Arabischen.</summary>
public static class Msg
{
    public static DialogResult Show(IWin32Window? owner, string text,
        MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.Information)
    {
        var options = L.Current.RightToLeft ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : 0;
        return MessageBox.Show(owner, ButtonNames(text), "Kahraman", buttons, icon, MessageBoxDefaultButton.Button1, options);
    }

    public static void Error(IWin32Window? owner, string text) => Show(owner, text, MessageBoxButtons.OK, MessageBoxIcon.Error);

    /// <summary>
    /// Die Knöpfe einer Meldung beschriftet Windows in seiner eigenen Sprache. Texte nennen sie deshalb über
    /// $YES$, $NO$ und $CANCEL$, ersetzt durch die Beschriftung, die Windows tatsächlich zeigt.
    /// </summary>
    public static string ButtonNames(string text) => text
        .Replace("$YES$", Label(5, "Ja"))
        .Replace("$NO$", Label(6, "Nein"))
        .Replace("$CANCEL$", Label(1, "Abbrechen"));

    static string Label(uint index, string fallback)
    {
        try
        {
            var p = MB_GetString(index);
            var s = p == IntPtr.Zero ? null : Marshal.PtrToStringUni(p);
            return string.IsNullOrEmpty(s) ? fallback : s.Replace("&", "");
        }
        catch (EntryPointNotFoundException) { return fallback; }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr MB_GetString(uint button);
}

/// <summary>Spiegelt Fenster und Menüs für Sprachen, die von rechts nach links geschrieben werden.</summary>
public static class Rtl
{
    public static void Apply(Form form)
    {
        if (!L.Current.RightToLeft) return;
        form.RightToLeft = RightToLeft.Yes;
        form.RightToLeftLayout = true;
        Mirror(form);
    }

    public static void Apply(ToolStrip strip)
    {
        if (L.Current.RightToLeft) strip.RightToLeft = RightToLeft.Yes;
    }

    static void Mirror(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            switch (c)
            {
                case ListView lv: lv.RightToLeftLayout = true; break;
                case TabControl tc: tc.RightToLeftLayout = true; break;
                case DateTimePicker dp: dp.RightToLeftLayout = true; break;
                case TrackBar tb: tb.RightToLeftLayout = true; break;
            }
            if (c.ContextMenuStrip is { } menu) Apply(menu);
            Mirror(c);
        }
    }
}
