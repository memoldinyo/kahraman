using Kahraman.Core;

namespace Kahraman.Ui;

/// <summary>Auswertung des Logfiles nach Zeitraum und Tätigkeiten / Untertätigkeiten / Kommentaren.</summary>
public sealed class EvaluationForm : Form
{
    readonly AppServices app;
    readonly Func<IWin32Window, int, bool> openSettings;
    readonly ModernRadioButton[] periodButtons;
    readonly ModernRadioButton rbFree = new() { Text = L.T("eval.from"), AutoSize = true };
    readonly DateTimePicker dtFrom = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    readonly DateTimePicker dtTo = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    readonly ModernRadioButton rbAct = new() { Text = L.T("eval.levelActivities"), AutoSize = true, Checked = true };
    readonly ModernRadioButton rbSub = new() { Text = L.T("eval.levelSubs"), AutoSize = true };
    readonly ModernRadioButton rbComment = new() { Text = L.T("eval.levelComments"), AutoSize = true };
    readonly ListView list = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true, HideSelection = false };
    List<Span> spans = [];
    List<EvaluationRow> rows = [];
    PeriodKind kind = PeriodKind.Monat;
    bool updating;

    public EvaluationForm(AppServices app, Func<IWin32Window, int, bool> openSettings)
    {
        this.app = app;
        this.openSettings = openSettings;
        Text = L.T("eval.title");
        Icon = IconFactory.AppIcon;
        Font = Theme.Font;
        Size = new Size(720, 560);
        MinimumSize = new Size(560, 380);
        StartPosition = FormStartPosition.CenterScreen;

        string[] names = [L.T("eval.today"), L.T("eval.week"), L.T("eval.month"), L.T("eval.quarter"), L.T("eval.year")];
        periodButtons = names.Select((n, i) => new ModernRadioButton { Text = n, AutoSize = true, Tag = (PeriodKind)i, Margin = new Padding(3, 3, 24, 3) }).ToArray();

        var top = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(8, 8, 8, 0) };
        var row1 = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        row1.Controls.AddRange(periodButtons);
        top.Controls.Add(row1);

        var row2 = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        var prev = new ModernButton { Text = "◀", Width = 40, Height = 27 };
        var next = new ModernButton { Text = "▶", Width = 40, Height = 27 };
        var tips = new ToolTip();
        tips.SetToolTip(prev, L.T("eval.previous"));
        tips.SetToolTip(next, L.T("eval.next"));
        rbFree.Margin = new Padding(3, 6, 3, 3);
        row2.Controls.AddRange([rbFree, dtFrom, new Label { Text = L.T("eval.to"), AutoSize = true, Margin = new Padding(6, 7, 6, 3) }, dtTo, prev, next]);
        top.Controls.Add(row2);

        var row3 = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
        row3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row3.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var level = new GroupBox { Text = L.T("eval.level"), AutoSize = true, Dock = DockStyle.Fill };
        var levelFlow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        rbAct.Margin = rbSub.Margin = rbComment.Margin = new Padding(3, 3, 24, 3);
        levelFlow.Controls.AddRange([rbAct, rbSub, rbComment]);
        level.Controls.Add(levelFlow);
        var settings = new ModernButton { Text = L.T("tray.settings"), AutoSize = true, MinimumSize = new Size(120, 30), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        row3.Controls.Add(level, 0, 0);
        row3.Controls.Add(settings, 1, 0);
        top.Controls.Add(row3);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 3, Padding = new Padding(8) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var details = new ModernButton { Text = L.T("eval.details"), AutoSize = true, MinimumSize = new Size(0, 30), Anchor = AnchorStyles.Left | AnchorStyles.Right };
        var copy = new ModernButton { Text = L.T("eval.copy"), AutoSize = true, MinimumSize = new Size(100, 30) };
        var ok = new ModernButton { Text = L.T("button.ok"), AutoSize = true, MinimumSize = new Size(100, 30), DialogResult = DialogResult.OK };
        bottom.Controls.Add(details, 0, 0);
        bottom.Controls.Add(copy, 1, 0);
        bottom.Controls.Add(ok, 2, 0);
        tips.SetToolTip(copy, L.T("eval.copyTip"));
        CancelButton = ok;
        ok.Click += (_, _) => Close();

        var listPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 0) };
        listPanel.Controls.Add(list);
        Controls.Add(listPanel);
        Controls.Add(top);
        Controls.Add(bottom);

        foreach (var rb in periodButtons)
            rb.CheckedChanged += (_, _) => { if (rb.Checked && !updating) SetKind((PeriodKind)rb.Tag!); };
        rbFree.CheckedChanged += (_, _) => { if (rbFree.Checked && !updating) { kind = PeriodKind.Frei; Recalculate(); } };
        dtFrom.ValueChanged += (_, _) => DatesEdited();
        dtTo.ValueChanged += (_, _) => DatesEdited();
        prev.Click += (_, _) => Shift(-1);
        next.Click += (_, _) => Shift(1);
        foreach (var rb in new[] { rbAct, rbSub, rbComment })
            rb.CheckedChanged += (_, _) => { if (rb.Checked) Recalculate(); };
        settings.Click += (_, _) => { if (openSettings(this, 1)) Reload(); };
        details.Click += (_, _) => ShowDetails();
        copy.Click += (_, _) => CopyTable();
        list.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.C) { CopyTable(); e.Handled = true; }
            if (e.Control && e.KeyCode == Keys.A) { foreach (ListViewItem i in list.Items) i.Selected = i.Tag is EvaluationRow; e.Handled = true; }
        };
        list.DoubleClick += (_, _) => ShowDetails();

        app.Settings.Restore(this);
        Reload();
        updating = true;
        periodButtons[(int)PeriodKind.Monat].Checked = true;
        updating = false;
        SetKind(PeriodKind.Monat);
        Theme.Apply(this);
        Rtl.Apply(this);
    }

    void Reload()
    {
        app.Refresh();
        spans = Evaluation.Spans(app.Log.Entries);
        Recalculate();
    }

    void SetKind(PeriodKind k)
    {
        kind = k;
        SetPeriod(Period.Of(k, DateOnly.FromDateTime(DateTime.Today)));
    }

    void SetPeriod(Period p)
    {
        updating = true;
        dtFrom.Value = p.From.ToDateTime(TimeOnly.MinValue);
        dtTo.Value = p.To.ToDateTime(TimeOnly.MinValue);
        updating = false;
        Recalculate();
    }

    void DatesEdited()
    {
        if (updating) return;
        updating = true;
        foreach (var rb in periodButtons) rb.Checked = false;
        rbFree.Checked = true;
        updating = false;
        kind = PeriodKind.Frei;
        Recalculate();
    }

    void Shift(int direction) => SetPeriod(CurrentPeriod().Shift(kind, direction));

    Period CurrentPeriod()
    {
        var from = DateOnly.FromDateTime(dtFrom.Value);
        var to = DateOnly.FromDateTime(dtTo.Value);
        return to < from ? new Period(to, from) : new Period(from, to);
    }

    DetailLevel Level => rbComment.Checked ? DetailLevel.Kommentare : rbSub.Checked ? DetailLevel.Untertaetigkeiten : DetailLevel.Taetigkeiten;

    void Recalculate()
    {
        if (updating) return;
        var excluded = Evaluation.ExcludedSet(app.Settings.ExcludedActivities);
        var level = Level;
        rows = Evaluation.Aggregate(spans, CurrentPeriod(), level, excluded);

        list.BeginUpdate();
        list.Items.Clear();
        list.Columns.Clear();
        list.Columns.Add(L.T("col.activity"), 260);
        if (level >= DetailLevel.Untertaetigkeiten) list.Columns.Add(L.T("col.sub"), 180);
        if (level >= DetailLevel.Kommentare) list.Columns.Add(L.T("col.comment"), 180);
        list.Columns.Add(L.T("col.hours"), 80, HorizontalAlignment.Right);
        list.Columns.Add(L.T("col.days"), 70, HorizontalAlignment.Right);

        foreach (var r in rows)
            list.Items.Add(new ListViewItem(Cells(r.Activity, r.Sub, r.Comment, r.Hours, level)) { Tag = r });
        var sum = rows.Sum(r => r.Hours);
        var total = new ListViewItem(Cells(L.T("eval.total"), "", "", sum, level))
        {
            ForeColor = Theme.Current.Claret,
            Font = new Font(list.Font, FontStyle.Bold),
        };
        list.Items.Add(total);
        list.EndUpdate();
    }

    static string[] Cells(string act, string sub, string comment, double hours, DetailLevel level)
    {
        var cells = new List<string> { act };
        if (level >= DetailLevel.Untertaetigkeiten) cells.Add(sub);
        if (level >= DetailLevel.Kommentare) cells.Add(comment);
        cells.Add(Evaluation.FormatHours(hours));
        cells.Add(Evaluation.FormatHours(hours / Evaluation.HoursPerDay));
        return cells.ToArray();
    }

    void CopyTable()
    {
        var level = Level;
        var lines = new List<string> { string.Join("\t", list.Columns.Cast<ColumnHeader>().Select(c => c.Text)) };
        var selected = list.SelectedItems.Cast<ListViewItem>().ToList();
        IEnumerable<ListViewItem> items = selected.Count > 1 ? selected : list.Items.Cast<ListViewItem>();
        foreach (var item in items)
            lines.Add(string.Join("\t", item.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text)));
        Clipboard.SetText(string.Join("\r\n", lines) + "\r\n");
    }

    void ShowDetails()
    {
        var period = CurrentPeriod();
        var selected = list.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag).OfType<EvaluationRow>()
            .Select(r => r.Activity).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var detailRows = Evaluation.Details(spans, period, Evaluation.ExcludedSet(app.Settings.ExcludedActivities), selected);
        var title = L.F("eval.detailsTitle", period.From.ToDateTime(TimeOnly.MinValue), period.To.ToDateTime(TimeOnly.MinValue)) +
                    (selected.Count == 0 ? " " + L.T("eval.detailsAll") : selected.Count == 1 ? $" – {selected[0]}" : " – " + L.F("eval.detailsCount", selected.Count));
        var f = new TextForm(title, Evaluation.DetailsAsText(detailRows), app.Settings, saveable: true);
        f.Show(this);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        app.Settings.Remember(this);
        base.OnFormClosing(e);
    }
}
