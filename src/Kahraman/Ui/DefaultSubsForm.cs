using Kahraman.Core;

namespace Kahraman.Ui;

/// <summary>Standarduntertätigkeiten editieren: priorisieren, löschen, "Hinzufügen".</summary>
public sealed class DefaultSubsForm : Form
{
    readonly AppServices app;
    readonly ItemListBox list = new() { Dock = DockStyle.Fill };
    readonly TextBox txtNew = new() { Dock = DockStyle.Fill };

    public DefaultSubsForm(AppServices app)
    {
        this.app = app;
        Text = L.T("templates.title");
        Icon = IconFactory.AppIcon;
        Font = Theme.Font;
        Size = new Size(380, 420);
        MinimumSize = new Size(300, 300);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;

        var g = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(8) };
        g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        g.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        g.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        g.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        g.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        g.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var hint = new Label { Text = L.T("templates.hint"), AutoSize = true, Margin = new Padding(3, 3, 3, 6) };
        g.Controls.Add(hint, 0, 0);
        g.SetColumnSpan(hint, 2);
        g.Controls.Add(list, 0, 1);
        g.SetColumnSpan(list, 2);
        var add = new ModernButton { Text = L.T("templates.add"), AutoSize = true, MinimumSize = new Size(90, 26) };
        g.Controls.Add(txtNew, 0, 2);
        g.Controls.Add(add, 1, 2);
        var close = new ModernButton { Text = L.T("button.ok"), AutoSize = true, MinimumSize = new Size(100, 30), Anchor = AnchorStyles.Right, DialogResult = DialogResult.OK };
        g.Controls.Add(close, 0, 3);
        g.SetColumnSpan(close, 2);
        Controls.Add(g);
        CancelButton = close;

        add.Click += (_, _) => Add();
        txtNew.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Add(); } };
        list.MouseDown += ListMouseDown;
        list.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete && list.SelectedListItem is { } i) Delete(i.Text); };
        Fill();
        Theme.Apply(this);
        Rtl.Apply(this);
    }

    void Fill() => list.SetItems(app.Store.Defaults
        .OrderBy(d => d.Text, StringComparer.CurrentCultureIgnoreCase)
        .Select(d => new ListItem(d.Text, d.Status)));

    void Add()
    {
        var text = txtNew.Text.Trim();
        if (text.Length == 0) return;
        Run(() => app.ModifyStore(s =>
        {
            if (!s.Defaults.Any(d => string.Equals(d.Text.Trim(), text, StringComparison.OrdinalIgnoreCase)))
                s.Defaults.Add(new Item(text));
        }));
        txtNew.Clear();
        Fill();
        list.SelectedIndex = list.IndexOfText(text);
    }

    void Delete(string text)
    {
        Run(() => app.ModifyStore(s => s.Defaults.RemoveAll(d => d.Text == text)));
        Fill();
    }

    void ListMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        int idx = list.IndexFromPoint(e.Location);
        if (idx < 0) return;
        list.SelectedIndex = idx;
        var item = (ListItem)list.Items[idx];
        var menu = new ContextMenuStrip();
        foreach (var style in StatusStyle.All)
        {
            var status = (int)style.Status;
            menu.Items.Add(new ToolStripMenuItem(style.Name, null, (_, _) =>
            {
                Run(() => app.ModifyStore(s => { foreach (var d in s.Defaults.Where(d => d.Text == item.Text)) d.Status = status; }));
                Fill();
            }) { Checked = item.Status == status, ForeColor = style.Fore });
        }
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.T("menu.delete"), null, (_, _) => Delete(item.Text));
        Rtl.Apply(menu);
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        menu.Show(list, e.Location);
    }

    void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is KahramanException or IOException or UnauthorizedAccessException)
        {
            Msg.Error(this, ex.Message);
        }
    }
}
