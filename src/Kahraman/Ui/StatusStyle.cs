using Kahraman.Core;

namespace Kahraman.Ui;

/// <summary>Darstellung der Priorisierungen in den Auswahllisten (Farben je nach Hell- oder Dunkelmodus).</summary>
public sealed record StatusStyle(ItemStatus Status, string NameKey, Color? LightFore, Color? LightBack, Color? DarkFore, Color? DarkBack, bool Bold)
{
    /// <summary>Bezeichnung im Kontextmenü (in der eingestellten Sprache).</summary>
    public string Name => L.T(NameKey);

    public Color Fore => (Theme.Current.Dark ? DarkFore : LightFore) ?? Theme.Current.Text;
    public Color? Back => Theme.Current.Dark ? DarkBack : LightBack;

    public static readonly StatusStyle[] All =
    [
        new(ItemStatus.Normal, "status.normal", null, null, null, null, false),
        new(ItemStatus.Allgemein, "status.general", Color.FromArgb(46, 125, 62), null, Color.FromArgb(123, 200, 143), null, false),
        new(ItemStatus.Projekt, "status.project", Color.FromArgb(122, 31, 61), Color.FromArgb(248, 227, 234), Color.FromArgb(242, 184, 200), Color.FromArgb(74, 33, 49), false),
        new(ItemStatus.Unwichtig, "status.unimportant", Color.FromArgb(154, 154, 154), null, Color.FromArgb(125, 125, 125), null, false),
        new(ItemStatus.Wichtig, "status.important", Color.FromArgb(122, 31, 61), null, Color.FromArgb(199, 115, 143), null, true),
        new(ItemStatus.Standard, "status.standard", Color.FromArgb(29, 95, 160), null, Color.FromArgb(142, 194, 240), null, false),
    ];

    public static StatusStyle Of(int status) =>
        status >= 0 && status < All.Length ? All[status] : All[0];
}
