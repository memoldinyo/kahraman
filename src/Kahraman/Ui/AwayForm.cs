using Kahraman.Core;

namespace Kahraman.Ui;

/// <summary>
/// Rückfrage nach einer Abwesenheit: die Arbeit bis zum Gehen eintragen, die Lücke als Pause buchen
/// (oder als Arbeit stehen lassen) bzw. am nächsten Tag den Arbeitsbeginn eintragen.
/// </summary>
public sealed class AwayForm : Form
{
    readonly AwayState away;
    readonly LogEntry? last;
    readonly DateTime back;
    readonly string breakActivity, startActivity;
    readonly ModernCheckBox chkWork = new() { AutoSize = true, Checked = true, Margin = new Padding(3, 12, 3, 3) };
    readonly ComboBox cbWork = new()
    {
        DropDownStyle = ComboBoxStyle.DropDown, Width = 340, Margin = new Padding(24, 3, 3, 3),
        AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems,
    };
    readonly ModernRadioButton rbPlanned = new() { AutoSize = true };
    readonly ModernRadioButton rbBack = new() { AutoSize = true };
    readonly ModernRadioButton rbWork = new() { AutoSize = true, Text = L.T("away.wasWork") };

    /// <summary>Die anzuhängenden Einträge (nach "Eintragen").</summary>
    public List<LogEntry> Entries { get; private set; } = [];

    public AwayForm(AppServices app, AwayState away, DateTime back, string breakActivity, string startActivity)
    {
        this.away = away;
        this.back = back;
        this.breakActivity = breakActivity;
        this.startActivity = startActivity;
        last = app.Log.Last;

        Text = L.T("away.title");
        Icon = IconFactory.AppIcon;
        Font = Theme.Font;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(14, 10, 14, 10);

        var g = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        var since = away.Since.Date == back.Date ? L.F("away.since", away.Since, Reason(away.Kind)) : L.F("away.sinceDate", away.Since, Reason(away.Kind));
        g.Controls.Add(new Label { Text = since, AutoSize = true, MaximumSize = new Size(460, 0), Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 3, 3, 6) });

        if (Away.NeedsWorkEntry(away, last))
        {
            chkWork.Text = L.F("away.work", away.Since);
            chkWork.CheckedChanged += (_, _) => cbWork.Enabled = chkWork.Checked;
            var names = new List<string>(Recent.Activities(app.Log.Entries, 15).Select(a => a.Trim()));
            names.AddRange(app.Store.Activities.Values.Select(a => a.Text.Trim()).Where(a => !names.Contains(a, StringComparer.OrdinalIgnoreCase)).Order());
            cbWork.Items.AddRange([.. names.Where(n => n.Length > 0)]);
            cbWork.Text = last!.Activity.Trim();
            g.Controls.Add(chkWork);
            g.Controls.Add(cbWork);
        }

        if (!Away.NewDay(away, back))
        {
            var gap = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 10, 0, 0) };
            if (Away.PlannedEndPassed(away, back))
            {
                rbPlanned.Text = L.F("away.pausePlanned", away.PlannedEnd!.Value);
                rbBack.Text = L.F("away.pauseNow", back);
                rbPlanned.Checked = true;
                gap.Controls.Add(rbPlanned);
            }
            else
            {
                rbBack.Text = L.F("away.pauseUntilBack", away.Since, back);
                rbBack.Checked = true;
            }
            gap.Controls.Add(rbBack);
            gap.Controls.Add(rbWork);
            g.Controls.Add(gap);
        }
        else if (last?.Time.Date != back.Date)
        {
            g.Controls.Add(new Label { Text = L.F("away.newDay", back), AutoSize = true, MaximumSize = new Size(460, 0), Margin = new Padding(3, 10, 3, 3) });
        }

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 14, 0, 0) };
        var skip = new ModernButton { Text = L.T("away.skip"), AutoSize = true, MinimumSize = new Size(110, 30), DialogResult = DialogResult.Cancel };
        var ok = new ModernButton { Text = L.T("away.ok"), AutoSize = true, MinimumSize = new Size(110, 30) };
        ok.Click += (_, _) => Confirm();
        buttons.Controls.AddRange([skip, ok]);
        g.Controls.Add(buttons);
        Controls.Add(g);
        AcceptButton = ok;
        CancelButton = skip;

        Theme.Apply(this);
        Rtl.Apply(this);
    }

    static string Reason(AwayKind kind) => kind switch
    {
        AwayKind.Lock => L.T("away.reasonLock"),
        AwayKind.SignOut => L.T("away.reasonSignOut"),
        AwayKind.Shutdown => L.T("away.reasonShutdown"),
        AwayKind.Sleep => L.T("away.reasonSleep"),
        AwayKind.Idle => L.T("away.reasonIdle"),
        AwayKind.Break => L.T("away.reasonBreak"),
        _ => L.T("away.reasonEndOfDay"),
    };

    void Confirm()
    {
        (string, string)? work = null;
        var activity = cbWork.Text.Trim();
        if (chkWork.Parent is not null && chkWork.Checked)
        {
            if (activity.Length == 0)
            {
                Msg.Show(this, L.T("input.activityRequired"));
                cbWork.Focus();
                return;
            }
            // Gleiche Tätigkeit wie zuletzt: deren Untertätigkeit übernehmen.
            var sub = last is not null && string.Equals(last.Activity.Trim(), activity, StringComparison.OrdinalIgnoreCase) ? last.Sub : "";
            work = (activity, sub);
        }
        var pause = rbPlanned.Checked ? PauseChoice.UntilPlanned : rbBack.Checked ? PauseChoice.UntilBack : PauseChoice.None;
        Entries = Away.Entries(away, last, back, work, pause, breakActivity, startActivity);
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        if (cbWork.Parent is not null) { cbWork.Focus(); cbWork.SelectAll(); }
    }
}
