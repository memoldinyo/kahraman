using System.Media;
using Kahraman.Core;

namespace Kahraman.Ui;

/// <summary>
/// Das Eingabefenster: Tätigkeit (1), Untertätigkeit (2), Kommentar (3).
/// Wird als Abfrage ("Popup") und zum Bearbeiten eines Logfile-Eintrags ("Edit") verwendet.
/// </summary>
public sealed class InputForm : Form
{
    public enum InputMode { Popup, Edit }

    sealed class SearchState { public string? Term; public string? LastResult; }

    readonly AppServices app;
    readonly InputMode mode;
    readonly DateTime? lowerBound, upperBound;
    readonly ItemListBox lstAct = new() { AccessibleName = L.T("input.activities"), Dock = DockStyle.Fill };
    readonly ItemListBox lstSub = new() { AccessibleName = L.T("input.subs"), Dock = DockStyle.Fill };
    readonly TextBox txtAct = new() { AccessibleName = L.T("col.activity"), Dock = DockStyle.Fill };
    readonly TextBox txtSub = new() { AccessibleName = L.T("col.sub"), Dock = DockStyle.Fill };
    readonly TextBox txtComment = new() { AccessibleName = L.T("col.comment"), PlaceholderText = L.T("input.commentPlaceholder"), Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = false };
    // Feste Höhe für eine Zeile: das Fenster ändert seine Größe nicht, die Listen behalten den Platz.
    readonly Label lblQuestion = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, AutoSize = false, AutoEllipsis = true };
    readonly Label lblLast = new() { AutoSize = false, AutoEllipsis = true, Dock = DockStyle.Fill };
    readonly Label lblPath = new() { AutoSize = false, AutoEllipsis = true, Dock = DockStyle.Fill, ForeColor = Theme.Current.Muted };
    readonly DateTimePicker dtDate = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    readonly DateTimePicker dtTime = new() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 95 };
    readonly TrackBar track = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 1000, TickFrequency = 50, TickStyle = TickStyle.BottomRight, AutoSize = false, Height = 32 };
    // Feste Größe: der Knopftext wechselt, der Knopf nicht.
    readonly ModernButton btnOk = new() { AutoSize = false, Size = new Size(120, 30), AutoEllipsis = true };
    readonly ModernButton btnCancel = new() { Text = L.T("button.cancel"), AutoSize = true, MinimumSize = new Size(100, 30), DialogResult = DialogResult.Cancel };
    // Leiste rechts zum Ein-/Ausklappen des Kommentarfelds: eingeklappt bekommen die beiden Listen die ganze Breite.
    readonly CommentStrip strip = new() { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 2) };
    // Dasselbe für die Untertätigkeiten (Liste und Feld); der zuletzt gewählte Zustand bleibt gespeichert.
    readonly CommentStrip subStrip = new("strip.subCollapse", "strip.subExpand") { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 2) };
    readonly ToolTip commentTip = new();
    // Runde Rahmen um Listen und Eingabefelder; das Kommentarfeld wird samt Rahmen ein- und ausgeblendet.
    readonly FieldFrame actFrame, subFrame, commentFrame, actBox, subBox;
    bool commentTyped;      // Kommentar in diesem Fenster selbst getippt: nicht mehr automatisch ersetzen
    TableLayoutPanel grid = null!;
    readonly System.Windows.Forms.Timer tick = new() { Interval = 1000 };
    readonly SearchState actSearch = new(), subSearch = new();
    readonly DateTime sliderMin, sliderMax;
    bool updating;

    // Abfrage-Verhalten: Kahraman blendet sich wieder ein, bis man ihn einmal angeklickt hat.
    bool userTouched;
    DateTime programmaticActivation;
    // Die Abfrage lässt sich nicht schließen (kein X, kein Alt+F4, kein Esc) – nur mit einem Eintrag oder beim Beenden.
    bool closeAllowed;
    DateTime? nagAt;

    public LogEntry? Result { get; private set; }

    public InputForm(AppServices app, InputMode mode, LogEntry? initial = null, DateTime? lowerBound = null, DateTime? upperBound = null)
    {
        this.app = app;
        this.mode = mode;
        this.lowerBound = lowerBound;
        this.upperBound = upperBound;
        initial ??= app.Log.Last;

        Text = mode == InputMode.Popup ? Quips.Title(Random.Shared) : L.T("edit.title");
        Icon = IconFactory.AppIcon;
        Font = Theme.Font;
        KeyPreview = true;
        MinimumSize = new Size(560, 260);
        Size = new Size(720, 330);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = mode == InputMode.Popup;
        // Die Abfrage lässt sich nur minimieren, wenn das in den Einstellungen erlaubt ist.
        MinimizeBox = mode == InputMode.Popup && app.Settings.AllowMinimize;
        MaximizeBox = false;
        HelpButton = false;

        lblQuestion.Text = mode == InputMode.Popup ? Quips.Question(DateTime.Now, app.Log.Last?.Activity, Random.Shared) : L.T("edit.question");
        lblQuestion.Height = Font.Height + LogicalToDeviceUnits(10);
        lblLast.Height = lblPath.Height = Font.Height + 1;
        btnOk.Text = mode == InputMode.Popup ? Quips.OkText(Random.Shared) : L.T("button.save");
        btnOk.Click += (_, _) => Confirm();
        AcceptButton = btnOk;
        if (mode == InputMode.Edit) CancelButton = btnCancel;

        var listPad = new Padding(LogicalToDeviceUnits(3));
        var boxPad = new Padding(LogicalToDeviceUnits(8), LogicalToDeviceUnits(5), LogicalToDeviceUnits(8), LogicalToDeviceUnits(4));
        actFrame = new FieldFrame(lstAct, listPad);
        subFrame = new FieldFrame(lstSub, listPad);
        commentFrame = new FieldFrame(txtComment, boxPad);
        actBox = new FieldFrame(txtAct, boxPad);
        subBox = new FieldFrame(txtSub, boxPad);

        grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 4, Padding = new Padding(LogicalToDeviceUnits(8), LogicalToDeviceUnits(4), LogicalToDeviceUnits(8), LogicalToDeviceUnits(6)) };
        // Spalten: Tätigkeit | Untertätigkeit | Leiste | Kommentar | Leiste (die Breiten setzt ApplyLayout).
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LogicalToDeviceUnits(22)));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LogicalToDeviceUnits(22)));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        grid.Controls.Add(lblQuestion, 0, 0);
        grid.SetColumnSpan(lblQuestion, 5);
        grid.Controls.Add(actFrame, 0, 1);
        grid.Controls.Add(subFrame, 1, 1);
        grid.Controls.Add(subStrip, 2, 1);
        grid.SetRowSpan(subStrip, 2);
        grid.Controls.Add(commentFrame, 3, 1);
        grid.SetRowSpan(commentFrame, 2);
        grid.Controls.Add(strip, 4, 1);
        grid.SetRowSpan(strip, 2);
        grid.Controls.Add(actBox, 0, 2);
        grid.Controls.Add(subBox, 1, 2);
        var bottom = mode == InputMode.Popup ? BuildPopupBottom() : BuildEditBottom();
        grid.Controls.Add(bottom, 0, 3);
        grid.SetColumnSpan(bottom, 5);
        foreach (Control c in new Control[] { actFrame, subFrame, actBox, subBox, commentFrame }) c.Margin = new Padding(LogicalToDeviceUnits(3));
        Controls.Add(grid);

        var tips = new ToolTip();
        tips.SetToolTip(txtAct, L.T("input.tipActivity"));
        tips.SetToolTip(lstAct, L.T("input.tipActivities"));
        tips.SetToolTip(lstSub, L.T("input.tipSubs"));
        tips.SetToolTip(lblQuestion, lblQuestion.Text);

        WireEvents();

        updating = true;
        txtAct.Text = initial?.Activity ?? "";
        txtSub.Text = initial?.Sub ?? "";
        txtComment.Text = initial?.Comment ?? "";
        updating = false;
        FillActivities();
        strip.Expanded = !app.Settings.CommentOnlyWhenFilled || txtComment.Text.Trim().Length > 0;
        strip.Toggled += (_, _) => CommentToggled();
        txtComment.TextChanged += (_, _) =>
        {
            if (!updating) commentTyped = true;
            UpdateCommentToggle();
        };
        subStrip.Expanded = app.Settings.SubsExpanded || txtSub.Text.Trim().Length > 0;
        subStrip.Toggled += (_, _) => SubsToggled();
        txtSub.TextChanged += (_, _) => UpdateSubToggle();
        ApplyLayout();

        if (mode == InputMode.Edit)
        {
            var t = initial?.Time ?? DateTime.Now;
            sliderMin = lowerBound ?? t.AddHours(-12);
            sliderMax = upperBound ?? DateTime.Now;
            if (sliderMax <= sliderMin) sliderMax = sliderMin.AddSeconds(1);
            SetTime(t);
        }

        app.Settings.Restore(this, "InputForm." + mode);
        Theme.Apply(this);
        Rtl.Apply(this);
    }

    Control BuildPopupBottom()
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, AutoSize = true };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        p.Controls.Add(lblLast, 0, 0);
        p.Controls.Add(lblPath, 0, 1);
        p.Controls.Add(btnOk, 1, 0);
        p.SetRowSpan(btnOk, 2);
        btnOk.Anchor = AnchorStyles.Right;
        lblPath.Text = app.LogPath;
        new ToolTip().SetToolTip(lblPath, app.LogPath);
        UpdateLastLabel();
        tick.Tick += (_, _) => { UpdateLastLabel(); Nag(); };
        return p;
    }

    Control BuildEditBottom()
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, AutoSize = true };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        dtDate.Anchor = dtTime.Anchor = AnchorStyles.Left;
        p.Controls.Add(dtDate, 0, 0);
        p.Controls.Add(dtTime, 1, 0);
        p.Controls.Add(track, 2, 0);
        p.Controls.Add(btnOk, 3, 0);
        p.Controls.Add(btnCancel, 4, 0);
        dtDate.ValueChanged += (_, _) => TimeControlsChanged();
        dtTime.ValueChanged += (_, _) => TimeControlsChanged();
        track.Scroll += (_, _) =>
        {
            var span = sliderMax - sliderMin;
            SetTime(sliderMin + TimeSpan.FromSeconds(Math.Round(span.TotalSeconds * track.Value / track.Maximum)));
        };
        return p;
    }

    void CommentToggled()
    {
        ApplyLayout();
        if (strip.Expanded) txtComment.Focus();
        else if (txtComment.ContainsFocus || !ContainsFocus) txtAct.Focus();
    }

    /// <summary>Von Hand auf- oder zugeklappt: so bleibt es, auch beim nächsten Mal.</summary>
    void SubsToggled()
    {
        app.Settings.SubsExpanded = subStrip.Expanded;
        app.Settings.Save();
        bool hadFocus = subFrame.ContainsFocus || subBox.ContainsFocus;
        ApplyLayout();
        if (subStrip.Expanded) txtSub.Focus();
        else if (hadFocus || !ContainsFocus) txtAct.Focus();
    }

    /// <summary>Andere Tätigkeit: Untertätigkeiten zeigen, wenn eine vorgeschlagen wird oder sie zuletzt aufgeklappt waren.</summary>
    void ShowSubsIfNeeded()
    {
        bool show = app.Settings.SubsExpanded || txtSub.Text.Trim().Length > 0;
        if (subStrip.Expanded == show) { UpdateSubToggle(); return; }
        subStrip.Expanded = show;
        ApplyLayout();
    }

    /// <summary>Sichtbare Spalten teilen sich die Breite; eingeklappte bekommen keinen Platz, die Fenstergröße bleibt.</summary>
    void ApplyLayout()
    {
        bool subs = subStrip.Expanded, comment = strip.Expanded;
        float share = 100f / (1 + (subs ? 1 : 0) + (comment ? 1 : 0));
        grid.SuspendLayout();
        grid.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, share);
        grid.ColumnStyles[1] = subs ? new ColumnStyle(SizeType.Percent, share) : new ColumnStyle(SizeType.Absolute, 0);
        grid.ColumnStyles[3] = comment ? new ColumnStyle(SizeType.Percent, share) : new ColumnStyle(SizeType.Absolute, 0);
        subFrame.Visible = subBox.Visible = subs;
        commentFrame.Visible = comment;
        grid.ResumeLayout();
        UpdateCommentToggle();
        UpdateSubToggle();
    }

    void UpdateSubToggle()
    {
        var sub = txtSub.Text.Trim();
        subStrip.HasText = sub.Length > 0;
        commentTip.SetToolTip(subStrip, subStrip.Expanded
            ? L.T("input.tipSubCollapse")
            : sub.Length > 0 ? L.T("input.tipSubExpand") + "\n" + L.T("col.sub") + ": " + sub : L.T("input.tipSubExpand"));
    }

    /// <summary>Anderes Projekt / andere Untertätigkeit: dessen letzten Kommentar vorschlagen und das Feld
    /// aufklappen, wenn etwas darin steht (sonst wieder einklappen, sofern so eingestellt).</summary>
    void SuggestComment()
    {
        // Beim Bearbeiten gehört der Kommentar zum Eintrag und wird nicht ausgetauscht.
        if (!commentTyped && mode == InputMode.Popup)
        {
            updating = true;
            txtComment.Text = Recent.Comment(app.Log.Entries, txtAct.Text, txtSub.Text);
            updating = false;
        }
        bool show = txtComment.Text.Trim().Length > 0;
        if (app.Settings.CommentOnlyWhenFilled && strip.Expanded != show)
        {
            strip.Expanded = show;
            ApplyLayout();
        }
        else UpdateCommentToggle();
    }

    void UpdateCommentToggle()
    {
        bool hasText = txtComment.Text.Trim().Length > 0;
        strip.HasText = hasText;
        commentTip.SetToolTip(strip, strip.Expanded
            ? L.T("input.tipCollapse")
            : hasText ? L.T("input.tipExpand") + "\n" + L.T("col.comment") + ": " + txtComment.Text.Trim() : L.T("input.tipExpand"));
    }

    void WireEvents()
    {
        lstAct.SelectedIndexChanged += (_, _) =>
        {
            if (updating || lstAct.SelectedListItem is not { } item) return;
            updating = true;
            txtAct.Text = item.Text;
            updating = false;
            ActivityChangedByUser();
        };
        txtAct.TextChanged += (_, _) =>
        {
            if (updating) return;
            SelectMatching(lstAct, txtAct.Text);
            ActivityChangedByUser();
        };
        lstSub.SelectedIndexChanged += (_, _) =>
        {
            if (updating || lstSub.SelectedListItem is not { } item) return;
            updating = true;
            txtSub.Text = item.Text;
            updating = false;
            SuggestComment();
        };
        txtSub.TextChanged += (_, _) =>
        {
            if (updating) return;
            SelectMatching(lstSub, txtSub.Text);
            SuggestComment();
        };
        lstAct.DoubleClick += (_, _) => { if (subBox.Visible) txtSub.Focus(); else if (commentFrame.Visible) txtComment.Focus(); else btnOk.Focus(); };
        lstSub.DoubleClick += (_, _) => { if (commentFrame.Visible) txtComment.Focus(); else btnOk.Focus(); };

        txtAct.KeyDown += (_, e) => TextBoxKeys(e, lstAct, txtAct, actSearch);
        txtSub.KeyDown += (_, e) => TextBoxKeys(e, lstSub, txtSub, subSearch);
        lstAct.MouseDown += (_, e) => ListMouseDown(e, lstAct, isSub: false);
        lstSub.MouseDown += (_, e) => ListMouseDown(e, lstSub, isSub: true);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F1) { e.Handled = true; HelpText.Show(this); }
            else if (e.KeyCode == Keys.K && e.Modifiers == Keys.Alt) { e.Handled = e.SuppressKeyPress = true; strip.Toggle(); }
            else if (e.KeyCode == Keys.U && e.Modifiers == Keys.Alt) { e.Handled = e.SuppressKeyPress = true; subStrip.Toggle(); }
        };
    }

    void TextBoxKeys(KeyEventArgs e, ItemListBox list, TextBox box, SearchState search)
    {
        switch (e.KeyCode)
        {
            case Keys.F3:
                e.Handled = e.SuppressKeyPress = true;
                bool continueSearch = search.Term is not null && search.LastResult == box.Text;
                if (!continueSearch) search.Term = box.Text.Trim();
                int idx = list.FindSubstring(search.Term!, continueSearch ? list.SelectedIndex : -1);
                if (idx < 0) { SystemSounds.Beep.Play(); return; }
                list.SelectedIndex = idx;
                search.LastResult = box.Text;
                box.SelectAll();
                break;
            case Keys.Down when list.Items.Count > 0:
                e.Handled = e.SuppressKeyPress = true;
                list.SelectedIndex = Math.Min(list.Items.Count - 1, list.SelectedIndex + 1);
                box.SelectAll();
                break;
            case Keys.Up when list.Items.Count > 0:
                e.Handled = e.SuppressKeyPress = true;
                list.SelectedIndex = Math.Max(0, list.SelectedIndex - 1);
                box.SelectAll();
                break;
        }
    }

    /// <summary>Markiert den passenden Listeneintrag: genau gleich, sonst den ersten, der mit dem Text beginnt.</summary>
    void SelectMatching(ItemListBox list, string text)
    {
        updating = true;
        int idx = list.IndexOfText(text);
        if (idx < 0) idx = list.FindPrefix(text);
        if (idx >= 0) list.SelectAndShow(idx);
        else list.ClearSelected();
        updating = false;
    }

    /// <summary>Beim Tippen ergänzt das Feld den Namen (z. B. "ac" → "Acme …", Rest markiert).</summary>
    static void SetAutoComplete(TextBox box, IEnumerable<string> names)
    {
        var source = new AutoCompleteStringCollection();
        source.AddRange(names.Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray());
        box.AutoCompleteCustomSource = source;
        box.AutoCompleteSource = AutoCompleteSource.CustomSource;
        box.AutoCompleteMode = AutoCompleteMode.Append;
    }

    void FillActivities()
    {
        var recent = Recent.Activities(app.Log.Entries, app.Settings.RecentCount)
            .Select(t => new ListItem(t, app.StatusOf(t), recent: true)).ToList();
        if (recent.Count > 0) recent[^1].SeparatorBelow = true;
        var all = app.Store.Activities.Values
            .OrderBy(a => a.Text, StringComparer.CurrentCultureIgnoreCase)
            .Select(a => new ListItem(a.Text, a.Status));
        int top = lstAct.TopIndex;
        updating = true;
        lstAct.SetItems(recent.Concat(all));
        updating = false;
        SetAutoComplete(txtAct, lstAct.Items.Cast<ListItem>().Where(i => !i.Recent).Select(i => i.Text)
            .Concat(recent.Select(i => i.Text)));
        SelectMatching(lstAct, txtAct.Text);
        if (lstAct.SelectedIndex < 0 && top < lstAct.Items.Count) lstAct.TopIndex = top;
        FillSubs();
    }

    /// <summary>Andere Tätigkeit gewählt: die zuletzt dazu benutzte Untertätigkeit vorschlagen (oder leeren).</summary>
    void ActivityChangedByUser()
    {
        updating = true;
        txtSub.Text = Recent.Subs(app.Log.Entries, txtAct.Text, 1).FirstOrDefault() ?? "";
        updating = false;
        FillSubs();
        ShowSubsIfNeeded();
        SuggestComment();
    }

    void FillSubs()
    {
        var activity = txtAct.Text;
        var recent = Recent.Subs(app.Log.Entries, activity, app.Settings.RecentCount)
            .Select(t => new ListItem(t, app.StatusOf(activity, t), recent: true)).ToList();
        if (recent.Count > 0) recent[^1].SeparatorBelow = true;
        var subs = app.Store.Find(activity)?.Subs.Values
            .OrderBy(s => s.Text, StringComparer.CurrentCultureIgnoreCase)
            .Select(s => new ListItem(s.Text, s.Status)) ?? [];
        updating = true;
        lstSub.SetItems(recent.Concat(subs));
        updating = false;
        SetAutoComplete(txtSub, lstSub.Items.Cast<ListItem>().Where(i => !i.Recent).Select(i => i.Text)
            .Concat(recent.Select(i => i.Text)));
        SelectMatching(lstSub, txtSub.Text);
    }

    void ListMouseDown(MouseEventArgs e, ItemListBox list, bool isSub)
    {
        if (e.Button != MouseButtons.Right) return;
        int idx = list.IndexFromPoint(e.Location);
        if (idx >= 0) list.SelectedIndex = idx;
        var item = idx >= 0 ? (ListItem)list.Items[idx] : null;

        var menu = new ContextMenuStrip();
        if (item is not null)
        {
            foreach (var style in StatusStyle.All)
            {
                var mi = new ToolStripMenuItem(style.Name) { Checked = item.Status == (int)style.Status, ForeColor = style.Fore };
                if (style.Bold) mi.Font = new Font(menu.Font, FontStyle.Bold);
                var status = (int)style.Status;
                mi.Click += (_, _) => Guard(() => SetStatus(item.Text, isSub, status));
                menu.Items.Add(mi);
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("menu.delete"), null, (_, _) => Guard(() => DeleteItem(item.Text, isSub)));
        }
        if (isSub)
        {
            if (menu.Items.Count > 0) menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("menu.insertTemplates"), null, (_, _) => Guard(InsertDefaults));
            menu.Items.Add(L.T("menu.editTemplates"), null, (_, _) =>
            {
                using var f = new DefaultSubsForm(app);
                f.ShowDialog(this);
            });
        }
        if (menu.Items.Count == 0) return;
        Rtl.Apply(menu);
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        menu.Show(list, e.Location);
    }

    void SetStatus(string text, bool isSub, int status)
    {
        var activity = txtAct.Text;
        app.ModifyStore(s =>
        {
            if (isSub) s.AddSub(activity, text).Status = status;
            else s.Add(text).Status = status;
        });
        FillActivities();
    }

    void DeleteItem(string text, bool isSub)
    {
        if (Msg.Show(this, L.F(isSub ? "input.deleteSub" : "input.deleteActivity", text),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var activity = txtAct.Text;
        app.ModifyStore(s =>
        {
            if (!isSub) s.Remove(text);
            else if (s.Find(activity) is { } a && a.FindSub(text) is { } sub) a.Subs.Remove(sub.Text);
        });
        FillActivities();
    }

    void InsertDefaults()
    {
        if (app.Store.Defaults.Count == 0)
        {
            Msg.Show(this, L.T("input.noTemplates"));
            return;
        }
        if (app.Store.Find(txtAct.Text) is not { } act)
        {
            Msg.Show(this, L.T("input.selectActivityFirst"));
            return;
        }
        int added = 0;
        app.ModifyStore(s => added = s.InsertDefaults(act.Text));
        FillSubs();
        if (added == 0) SystemSounds.Asterisk.Play();
    }

    void Confirm() => Guard(ConfirmCore);

    void ConfirmCore()
    {
        var activity = txtAct.Text.Trim();
        if (activity.Length == 0)
        {
            Msg.Show(this, L.T("input.activityRequired"));
            txtAct.Focus();
            return;
        }
        var existing = app.Store.Find(activity);
        if (existing is null)
        {
            var answer = Msg.Show(this, L.F("input.newActivity", activity), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel) { txtAct.Focus(); return; }
            if (answer == DialogResult.Yes) app.ModifyStore(s => s.Add(activity));
            existing = app.Store.Find(activity);
        }
        var activityText = existing?.Text ?? Recent.Activities(app.Log.Entries, int.MaxValue)
            .FirstOrDefault(a => string.Equals(a.Trim(), activity, StringComparison.OrdinalIgnoreCase)) ?? activity;

        var sub = txtSub.Text.Trim();
        var subText = sub;
        if (sub.Length > 0)
        {
            var existingSub = app.Store.Find(activityText)?.FindSub(sub);
            if (existingSub is null)
            {
                var answer = Msg.Show(this, L.F("input.newSub", sub, activityText.Trim()), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel) { txtSub.Focus(); return; }
                if (answer == DialogResult.Yes) app.ModifyStore(s => s.AddSub(activityText, sub));
                existingSub = app.Store.Find(activityText)?.FindSub(sub);
            }
            subText = existingSub?.Text ?? sub;
        }

        DateTime time;
        if (mode == InputMode.Popup)
        {
            var now = DateTime.Now;
            time = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
        }
        else
        {
            time = CurrentTime();
            var min = lowerBound;
            var max = upperBound ?? DateTime.Now;
            if ((min.HasValue && time < min.Value) || time > max)
            {
                Msg.Show(this, min.HasValue ? L.F("edit.timeBetween", min.Value, max) : L.F("edit.timeBefore", max));
                return;
            }
        }

        Result = new LogEntry(time, activityText, subText, txtComment.Text.Trim());
        if (mode == InputMode.Popup) app.Record(Result);
        closeAllowed = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    bool Guard(Action action)
    {
        try { action(); return true; }
        catch (Exception ex) when (ex is KahramanException or IOException or UnauthorizedAccessException)
        {
            Msg.Error(this, ex.Message);
            return false;
        }
    }

    // ---- Zeit (nur beim Bearbeiten) ----

    DateTime CurrentTime() => dtDate.Value.Date + dtTime.Value.TimeOfDay - TimeSpan.FromTicks(dtTime.Value.TimeOfDay.Ticks % TimeSpan.TicksPerSecond);

    void SetTime(DateTime t)
    {
        updating = true;
        dtDate.Value = t.Date;
        dtTime.Value = t;
        updating = false;
        UpdateSlider(t);
    }

    void TimeControlsChanged()
    {
        if (!updating) UpdateSlider(CurrentTime());
    }

    void UpdateSlider(DateTime t)
    {
        var span = (sliderMax - sliderMin).TotalSeconds;
        var pos = span <= 0 ? 0 : (t - sliderMin).TotalSeconds / span * track.Maximum;
        track.Value = (int)Math.Clamp(Math.Round(pos), track.Minimum, track.Maximum);
    }

    // ---- Abfrage-Fenster ----

    void UpdateLastLabel()
    {
        if (app.Log.Last is not { } last) { lblLast.Text = L.T("input.noEntryYet"); return; }
        var d = DateTime.Now - last.Time;
        if (d < TimeSpan.Zero) d = TimeSpan.Zero;
        lblLast.Text = L.F("input.sinceLast", $"{(int)d.TotalHours}:{d.Minutes:00}:{d.Seconds:00}", last.Time);
    }

    /// <summary>Holt das Fenster in den Vordergrund (z. B. bei Fälligkeit oder Hotkey).</summary>
    public void BringUp()
    {
        programmaticActivation = DateTime.Now;
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        TopMost = true;
        if (!Visible) Show();
        ForceForeground();
        Activate();
        txtAct.Focus();
        txtAct.SelectAll();
    }

    // Wie beim Vorbild: Die Abfrage bleibt über allen anderen Fenstern, bis sie beantwortet ist.
    // Blinken und Rütteln sind wahlweise (Einstellungen > Erinnerung).
    TimeSpan ShakeEvery => TimeSpan.FromMinutes(app.Settings.ShakeMinutes);
    static readonly TimeSpan KeyGuard = TimeSpan.FromSeconds(1.5);
    DateTime nextShake;

    void Nag()
    {
        if (mode != InputMode.Popup || ContainsFocus || !Enabled) return; // !Enabled: eigene Rückfrage ist offen
        if (MinimizedByUser) return; // blinkt in der Taskleiste, bis man es wieder öffnet
        StayOnTop();
        if (!userTouched)
        {
            // Noch nie angeklickt: alle 8 Sekunden wieder nach vorn (auch wenn Windows es beim ersten Mal verhindert hat).
            nagAt ??= DateTime.Now.AddSeconds(8);
            if (DateTime.Now < nagAt) return;
            nagAt = DateTime.Now.AddSeconds(8);
            BringUp();
            StartFlashing();
            return;
        }
        if (!app.Settings.Shake || DateTime.Now < nextShake) return;
        nextShake = DateTime.Now + ShakeEvery;
        Shake();
    }

    bool MinimizedByUser => MinimizeBox && WindowState == FormWindowState.Minimized;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (MinimizedByUser) FlashTaskbarUntilOpened();
    }

    void FlashTaskbarUntilOpened()
    {
        var info = new FLASHWINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<FLASHWINFO>(), hwnd = Handle, dwFlags = FLASHW_TRAY | FLASHW_TIMERNOFG };
        FlashWindowEx(ref info);
    }

    /// <summary>Hält das Fenster ganz oben (auch über anderen "immer im Vordergrund"-Fenstern), ohne den Fokus zu stehlen.</summary>
    void StayOnTop()
    {
        if (WindowState == FormWindowState.Minimized) ShowWindow(Handle, SW_SHOWNOACTIVATE);
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>Windows lässt Hintergrundprogramme normalerweise nicht nach vorn; über das Anhängen an den
    /// Eingabe-Thread des aktuellen Vordergrundfensters klappt es trotzdem.</summary>
    void ForceForeground()
    {
        var fg = GetForegroundWindow();
        if (fg == Handle) return;
        uint fgThread = GetWindowThreadProcessId(fg, out _), me = GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
        try
        {
            BringWindowToTop(Handle);
            SetForegroundWindow(Handle);
        }
        finally
        {
            if (attached) AttachThreadInput(me, fgThread, false);
        }
    }

    /// <summary>Lässt Titelleiste und/oder Taskleisten-Symbol blinken, bis das Fenster wieder vorn ist.</summary>
    void StartFlashing()
    {
        uint flags = (app.Settings.FlashCaption ? FLASHW_CAPTION : 0) | (app.Settings.FlashTaskbar ? FLASHW_TRAY : 0);
        if (flags == 0) return;
        var info = new FLASHWINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<FLASHWINFO>(), hwnd = Handle, dwFlags = flags | FLASHW_TIMERNOFG };
        FlashWindowEx(ref info);
    }

    async void Shake()
    {
        if (app.Settings.ShakeSound) SystemSounds.Exclamation.Play();
        StartFlashing();
        var x = Left;
        foreach (var dx in new[] { 12, -12, 10, -10, 7, -7, 4, -4, 0 })
        {
            if (IsDisposed) return;
            Left = x + dx;
            await Task.Delay(35);
        }
    }

    /// <summary>Hat Kahraman gerade selbst den Fokus geholt, werden Tasten kurz verworfen –
    /// sonst landet z. B. ein in Firefox getipptes Enter als Bestätigung in der Abfrage.</summary>
    protected override bool ProcessKeyPreview(ref Message m)
    {
        const int WM_KEYDOWN = 0x100, WM_CHAR = 0x102, WM_SYSKEYDOWN = 0x104;
        if (mode == InputMode.Popup && DateTime.Now - programmaticActivation < KeyGuard
            && m.Msg is WM_KEYDOWN or WM_CHAR or WM_SYSKEYDOWN)
            return true;
        return base.ProcessKeyPreview(ref m);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (mode == InputMode.Popup && DateTime.Now - programmaticActivation < KeyGuard) return true;
        return base.ProcessCmdKey(ref msg, keyData);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }
    const uint FLASHW_CAPTION = 1, FLASHW_TRAY = 2, FLASHW_TIMERNOFG = 12;
    const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOACTIVATE = 0x10;
    const int SW_SHOWNOACTIVATE = 4;
    static readonly IntPtr HWND_TOPMOST = new(-1);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool FlashWindowEx(ref FLASHWINFO info);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr hWnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool AttachThreadInput(uint attach, uint attachTo, bool doAttach);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

    /// <summary>Schließt die Abfrage ohne Eintrag (Kahraman wird beendet).</summary>
    public void CloseByProgram()
    {
        closeAllowed = true;
        Close();
    }

    // Abfrage: X-Knopf ausgegraut (CS_NOCLOSE), damit ist auch Alt+F4 gesperrt.
    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_NOCLOSE = 0x200;
            var cp = base.CreateParams;
            if (mode == InputMode.Popup) cp.ClassStyle |= CS_NOCLOSE;
            return cp;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Schließen durch den Benutzer verhindern (X, Alt+F4, "Fenster schließen" in der Taskleiste).
        // Abmelden von Windows und der Task-Manager (kommt als TaskManagerClosing) dürfen schließen –
        // sonst würde der Task-Manager ganz Kahraman beenden.
        if (mode == InputMode.Popup && !closeAllowed && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
        base.OnFormClosing(e);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        tick.Start();
        if (mode == InputMode.Popup) BringUp();
        else { txtAct.Focus(); txtAct.SelectAll(); }
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (mode == InputMode.Popup && (DateTime.Now - programmaticActivation).TotalSeconds > 1.5) userTouched = true;
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (mode != InputMode.Popup) return;
        // Bleibt oben (TopMost) und blinkt (falls eingestellt); angeklickt und verlassen: regelmäßig rütteln
        // (falls eingestellt), nie angeklickt: nach 8 Sekunden wieder nach vorn holen.
        StartFlashing();
        if (userTouched) nextShake = DateTime.Now + ShakeEvery;
        else nagAt = DateTime.Now.AddSeconds(8);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        tick.Stop();
        tick.Dispose();
        app.Settings.Remember(this, "InputForm." + mode);
        base.OnFormClosed(e);
    }
}
