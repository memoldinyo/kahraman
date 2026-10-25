using Kahraman.Core;

namespace Kahraman.Ui;

/// <summary>Textanzeige mit "Alles kopieren" und "Speichern unter" (Details der Auswertung, Hilfe, Über Kahraman).</summary>
public sealed class TextForm : Form
{
    readonly Settings settings;
    readonly string key;

    public TextForm(string title, string text, Settings settings, bool saveable, string key = "TextForm")
    {
        this.settings = settings;
        this.key = key;
        Text = title;
        Icon = IconFactory.AppIcon;
        Font = Theme.Font;
        Size = new Size(760, 480);
        MinimumSize = new Size(400, 250);
        StartPosition = FormStartPosition.CenterParent;

        var box = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = !saveable,
            Font = saveable ? new Font("Consolas", 10f) : Theme.Font, BackColor = SystemColors.Window, Text = text,
        };
        box.HandleCreated += (_, _) => box.SelectionStart = 0;

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
        var close = new ModernButton { Text = L.T("button.close"), AutoSize = true, MinimumSize = new Size(100, 30) };
        close.Click += (_, _) => Close();
        if (saveable)
        {
            var copy = new ModernButton { Text = L.T("text.copyAll"), AutoSize = true, MinimumSize = new Size(110, 30) };
            copy.Click += (_, _) => { if (text.Length > 0) Clipboard.SetText(text); };
            var save = new ModernButton { Text = L.T("text.saveAs"), AutoSize = true, MinimumSize = new Size(110, 30) };
            save.Click += (_, _) =>
            {
                using var dlg = new SaveFileDialog { Filter = L.T("file.filterText"), Title = L.T("text.saveTitle"), FileName = L.T("text.saveFileName") };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    File.WriteAllText(dlg.FileName, text, new System.Text.UTF8Encoding(true));
            };
            bottom.Controls.AddRange([copy, save]);
        }
        bottom.Controls.Add(close);
        CancelButton = close;
        Controls.Add(box);
        Controls.Add(bottom);
        settings.Restore(this, key);
        Theme.Apply(this);
        Rtl.Apply(this);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        settings.Remember(this, key);
        base.OnFormClosing(e);
    }
}
