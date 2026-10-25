using Kahraman.Core;

namespace Kahraman.Ui;

/// <summary>Logfile bearbeiten: Einträge filtern, ändern, davor einfügen, löschen, ersetzen.</summary>
public sealed class LogEditorForm : Form
{
    readonly AppServices app;
    readonly ListView list = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true,
        VirtualMode = true, MultiSelect = false, HideSelection = false,
    };
    readonly TextBox txtFilter = new() { Width = 200 };
    readonly List<LogEntry> snapshot;
    List<LogEntry> work;
    List<int> view = [];
    bool dirty;

    public LogEditorForm(AppServices app)
    {
        this.app = app;
        app.Log.ReloadIfChanged();
        snapshot = app.Log.Entries.ToList();
        work = snapshot.ToList();

        Icon = IconFactory.AppIcon;
        Font = Theme.Font;
        Size = new Size(860, 520);
        MinimumSize = new Size(500, 300);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        list.Columns.Add(L.T("col.time"), 150);
        list.Columns.Add(L.T("col.activity"), 250);
        list.Columns.Add(L.T("col.sub"), 200);
        list.Columns.Add(L.T("col.comment"), 220);
        list.RetrieveVirtualItem += (_, e) =>
        {
            var en = work[view[e.ItemIndex]];
            e.Item = new ListViewItem([en.Time.ToString("G"), en.Activity, en.Sub, en.Comment]);
        };
        list.DoubleClick += (_, _) => Edit();
        list.KeyDown += ListKeyDown;
        list.KeyPress += (_, e) =>
        {
            // Tippen in der Liste filtert, Backspace löscht das letzte Zeichen.
            if (e.KeyChar == '\b') { if (txtFilter.Text.Length > 0) txtFilter.Text = txtFilter.Text[..^1]; e.Handled = true; }
            else if (!char.IsControl(e.KeyChar)) { txtFilter.Text += e.KeyChar; e.Handled = true; }
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem(L.T("editor.change"), null, (_, _) => Edit()) { ShortcutKeyDisplayString = L.T("key.ctrl") + "+E" });
        menu.Items.Add(new ToolStripMenuItem(L.T("editor.insertBefore"), null, (_, _) => InsertBefore()) { ShortcutKeyDisplayString = L.T("key.ctrl") + "+A" });
        menu.Items.Add(new ToolStripMenuItem(L.T("menu.delete"), null, (_, _) => Delete()) { ShortcutKeyDisplayString = L.T("key.delete") });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.T("editor.rename"), null, (_, _) => Replace());
        list.ContextMenuStrip = menu;

        txtFilter.TextChanged += (_, _) => ApplyFilter(SelectedWorkIndex());

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 4, Padding = new Padding(6) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var lbl = new Label { Text = L.T("editor.filter"), AutoSize = true, Anchor = AnchorStyles.Left };
        bottom.Controls.Add(lbl, 0, 0);
        txtFilter.Anchor = AnchorStyles.Left;
        bottom.Controls.Add(txtFilter, 1, 0);
        var ok = new ModernButton { Text = L.T("button.ok"), AutoSize = true, MinimumSize = new Size(100, 30) };
        var cancel = new ModernButton { Text = L.T("button.cancel"), AutoSize = true, MinimumSize = new Size(100, 30) };
        ok.Click += (_, _) => SaveAndClose();
        cancel.Click += (_, _) => Close();
        bottom.Controls.Add(ok, 2, 0);
        bottom.Controls.Add(cancel, 3, 0);
        CancelButton = cancel;

        Controls.Add(list);
        Controls.Add(bottom);

        var tips = new ToolTip();
        tips.SetToolTip(txtFilter, L.T("editor.filterTip"));

        app.Settings.Restore(this);
        Theme.Apply(this);
        Rtl.Apply(this);
        ApplyFilter(work.Count - 1);
    }

    void ApplyFilter(int keepWorkIndex)
    {
        var f = txtFilter.Text.Trim();
        view = Enumerable.Range(0, work.Count)
            .Where(i => f.Length == 0
                || work[i].Activity.Contains(f, StringComparison.CurrentCultureIgnoreCase)
                || work[i].Sub.Contains(f, StringComparison.CurrentCultureIgnoreCase)
                || work[i].Comment.Contains(f, StringComparison.CurrentCultureIgnoreCase))
            .ToList();
        list.VirtualListSize = view.Count;
        list.Invalidate();
        Text = $"{Path.GetFileName(app.LogPath)} - " + (f.Length > 0 ? L.F("editor.countFiltered", view.Count, work.Count) : L.F("editor.count", view.Count)) + (dirty ? " *" : "");

        int vi = view.BinarySearch(keepWorkIndex);
        if (vi < 0) vi = Math.Min(~vi, view.Count - 1);
        if (vi >= 0)
        {
            list.SelectedIndices.Clear();
            list.SelectedIndices.Add(vi);
            list.FocusedItem = list.Items[vi];
            list.EnsureVisible(vi);
        }
    }

    int SelectedWorkIndex() => list.SelectedIndices.Count > 0 ? view[list.SelectedIndices[0]] : -1;

    void ListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.E) Edit();
        else if (e.Control && e.KeyCode == Keys.A) InsertBefore();
        else if (e.KeyCode == Keys.Delete) Delete();
        else if (e.KeyCode == Keys.Enter) Edit();
        else return;
        e.Handled = e.SuppressKeyPress = true;
    }

    void Changed(int keepWorkIndex)
    {
        dirty = true;
        ApplyFilter(keepWorkIndex);
    }

    void Edit()
    {
        int wi = SelectedWorkIndex();
        if (wi < 0) return;
        DateTime? lower = wi > 0 ? work[wi - 1].Time : null;
        DateTime? upper = wi < work.Count - 1 ? work[wi + 1].Time : null;
        using var f = new InputForm(app, InputForm.InputMode.Edit, work[wi], lower, upper);
        if (f.ShowDialog(this) != DialogResult.OK || f.Result is null) return;
        work[wi] = f.Result;
        Changed(wi);
    }

    void InsertBefore()
    {
        int wi = SelectedWorkIndex();
        if (wi < 0) return;
        DateTime? lower = wi > 0 ? work[wi - 1].Time : null;
        var upper = work[wi].Time;
        var start = lower.HasValue ? lower.Value + (upper - lower.Value) / 2 : upper.AddMinutes(-1);
        start = start.AddTicks(-(start.Ticks % TimeSpan.TicksPerSecond));
        var initial = new LogEntry(start, work[wi].Activity, work[wi].Sub, "");
        using var f = new InputForm(app, InputForm.InputMode.Edit, initial, lower, upper) { Text = L.T("edit.insertBefore") };
        if (f.ShowDialog(this) != DialogResult.OK || f.Result is null) return;
        work.Insert(wi, f.Result);
        Changed(wi);
    }

    void Delete()
    {
        int wi = SelectedWorkIndex();
        if (wi < 0) return;
        var en = work[wi];
        if (Msg.Show(this, L.F("editor.deleteConfirm", en.Time, en.Activity, en.Sub),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        work.RemoveAt(wi);
        Changed(Math.Min(wi, work.Count - 1));
    }

    void Replace()
    {
        if (view.Count == 0) return;
        using var f = new ReplaceForm(app, view.Count, work[view[0]]);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        foreach (var i in view)
            work[i] = work[i] with { Activity = f.Activity, Sub = f.ReplaceSub ? f.Sub : work[i].Sub };
        Changed(SelectedWorkIndex());
    }

    void SaveAndClose()
    {
        if (!dirty) { Close(); return; }
        try
        {
            // Während der Bearbeitung kann Kahraman neue Einträge angehängt haben – diese übernehmen.
            app.Log.ReloadIfChanged();
            var disk = app.Log.Entries;
            if (disk.Count < snapshot.Count || !disk.Take(snapshot.Count).SequenceEqual(snapshot))
                throw new KahramanException(L.T("editor.changedOnDisk"));
            var merged = work.Concat(disk.Skip(snapshot.Count)).OrderBy(e => e.Time).ToList();
            app.SaveLog(merged);
            dirty = false;
            Close();
        }
        catch (Exception ex) when (ex is KahramanException or IOException or UnauthorizedAccessException)
        {
            Msg.Error(this, ex.Message);
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyFilter(work.Count - 1); // neueste Einträge zeigen
        list.Focus();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (dirty && e.CloseReason == CloseReason.UserClosing &&
            Msg.Show(this, L.T("editor.discard"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        app.Settings.Remember(this);
        base.OnFormClosing(e);
    }
}

/// <summary>Angezeigte Einträge ersetzen (Tätigkeit und optional Untertätigkeit).</summary>
public sealed class ReplaceForm : Form
{
    readonly ComboBox cbAct = new() { Dock = DockStyle.Fill, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    readonly ComboBox cbSub = new() { Dock = DockStyle.Fill, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    readonly ModernCheckBox chkSub = new() { Text = L.T("rename.alsoSub"), AutoSize = true, Checked = true };
    readonly AppServices app;

    public string Activity => cbAct.Text.Trim();
    public string Sub => cbSub.Text.Trim();
    public bool ReplaceSub => chkSub.Checked;

    public ReplaceForm(AppServices app, int count, LogEntry sample)
    {
        this.app = app;
        Text = L.T("rename.title");
        Icon = IconFactory.AppIcon;
        Font = Theme.Font;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var g = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(10), Dock = DockStyle.Fill };
        g.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        g.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        var info = new Label { Text = L.F("rename.info", count), AutoSize = true, Margin = new Padding(3, 3, 3, 10) };
        g.Controls.Add(info, 0, 0);
        g.SetColumnSpan(info, 2);
        g.Controls.Add(new Label { Text = L.T("rename.activity"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        g.Controls.Add(cbAct, 1, 1);
        g.Controls.Add(chkSub, 1, 2);
        g.Controls.Add(new Label { Text = L.T("rename.sub"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        g.Controls.Add(cbSub, 1, 3);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
        var ok = new ModernButton { Text = L.T("button.ok"), DialogResult = DialogResult.OK, MinimumSize = new Size(100, 30), AutoSize = true };
        var cancel = new ModernButton { Text = L.T("button.cancel"), DialogResult = DialogResult.Cancel, MinimumSize = new Size(100, 30), AutoSize = true };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        g.Controls.Add(buttons, 0, 4);
        g.SetColumnSpan(buttons, 2);
        Controls.Add(g);
        AcceptButton = ok;
        CancelButton = cancel;

        cbAct.Items.AddRange(app.Store.Activities.Values.Select(a => (object)a.Text).OrderBy(t => (string)t, StringComparer.CurrentCultureIgnoreCase).ToArray());
        cbAct.TextChanged += (_, _) => FillSubs();
        chkSub.CheckedChanged += (_, _) => cbSub.Enabled = chkSub.Checked;
        cbAct.Text = sample.Activity;
        cbSub.Text = sample.Sub;
        ok.Click += (_, _) =>
        {
            if (Activity.Length == 0) { DialogResult = DialogResult.None; cbAct.Focus(); }
        };
        Theme.Apply(this);
        Rtl.Apply(this);
    }

    void FillSubs()
    {
        var keep = cbSub.Text;
        cbSub.Items.Clear();
        if (app.Store.Find(cbAct.Text) is { } a)
            cbSub.Items.AddRange(a.Subs.Values.Select(s => (object)s.Text).OrderBy(t => (string)t, StringComparer.CurrentCultureIgnoreCase).ToArray());
        cbSub.Text = keep;
    }
}
