using Kahraman.Core;
using Microsoft.Win32;

namespace Kahraman.Ui;

/// <summary>Einstellungen: Allgemein, Auswertung, Hotkeys, Erinnerung.</summary>
public sealed class SettingsForm : Form
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValue = "Kahraman";

    readonly AppServices app;
    readonly Label lblDir = new() { AutoSize = true, MaximumSize = new Size(440, 0) };
    readonly TrackBar trackInterval = new() { Minimum = 1, Maximum = 240, TickFrequency = 10, Width = 300, AutoSize = false, Height = 36 };
    readonly NumericUpDown numInterval = new() { Minimum = 1, Maximum = 24 * 60, Width = 80 };
    readonly NumericUpDown numRecent = new() { Minimum = 0, Maximum = 50, Width = 60 };
    readonly ModernCheckBox chkCommentAuto = new() { Text = L.T("settings.commentAuto"), AutoSize = true };
    readonly ModernCheckBox chkAutostart = new() { Text = L.T("settings.autostart"), AutoSize = true };
    readonly ComboBox cbLanguage = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    readonly ListBox lbAll = new() { Dock = DockStyle.Fill, SelectionMode = SelectionMode.MultiExtended, Sorted = true };
    readonly ListBox lbIgnore = new() { Dock = DockStyle.Fill, SelectionMode = SelectionMode.MultiExtended, Sorted = true };
    readonly HotkeyBox hkFinish = new() { Width = 220 };
    readonly HotkeyBox hkEdit = new() { Width = 220 };
    readonly ModernCheckBox chkAllowMinimize = new() { Text = L.T("settings.allowMinimize"), AutoSize = true };
    readonly ModernCheckBox chkFlashCaption = new() { Text = L.T("settings.flashCaption"), AutoSize = true };
    readonly ModernCheckBox chkFlashTaskbar = new() { Text = L.T("settings.flashTaskbar"), AutoSize = true };
    readonly ModernCheckBox chkShake = new() { Text = L.T("settings.shake"), AutoSize = true, Anchor = AnchorStyles.Left };
    readonly NumericUpDown numShake = new() { Minimum = 1, Maximum = 60, Width = 60, Anchor = AnchorStyles.Left };
    readonly ModernCheckBox chkShakeSound = new() { Text = L.T("settings.shakeSound"), AutoSize = true };
    readonly HiddenTabControl tabs = new() { Dock = DockStyle.Fill };
    readonly SegmentedControl segTheme = new(
        ("", L.T("theme.system")), ("", L.T("theme.light")), ("", L.T("theme.dark")));
    string dataDir;

    public SettingsForm(AppServices app, int tab = 0)
    {
        this.app = app;
        var s = app.Settings;
        dataDir = s.DataDir ?? DefaultDataDir;

        Text = L.T("settings.title");
        Icon = IconFactory.AppIcon;
        Font = Theme.Font;
        Size = new Size(620, 530);
        MinimumSize = new Size(560, 480);
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = MinimizeBox = false;

        tabs.TabPages.Add(BuildGeneral());
        tabs.TabPages.Add(BuildEvaluation());
        tabs.TabPages.Add(BuildHotkeys());
        tabs.TabPages.Add(BuildReminder());
        tabs.SelectedIndex = Math.Clamp(tab, 0, 3);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(6) };
        var cancel = new ModernButton { Text = L.T("button.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(100, 30) };
        var ok = new ModernButton { Text = L.T("button.ok"), AutoSize = true, MinimumSize = new Size(100, 30) };
        ok.Click += (_, _) => Apply();
        bottom.Controls.AddRange([cancel, ok]);
        AcceptButton = ok;
        CancelButton = cancel;

        // Reiter wie in den Windows-11-Einstellungen: Text mit Akzentlinie statt Karteikarten.
        var tabPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 0, 6, 0) };
        tabPanel.Controls.Add(tabs);
        tabPanel.Controls.Add(new PivotHeader(tabs));
        Controls.Add(tabPanel);
        Controls.Add(bottom);

        lblDir.Text = dataDir;
        numInterval.Value = s.IntervalMinutes;
        trackInterval.Value = Math.Clamp(s.IntervalMinutes, trackInterval.Minimum, trackInterval.Maximum);
        numRecent.Value = s.RecentCount;
        chkAutostart.Checked = IsAutostartEnabled();
        chkCommentAuto.Checked = s.CommentOnlyWhenFilled;
        hkFinish.Hotkey = s.HotkeyFinish;
        hkEdit.Hotkey = s.HotkeyEdit;
        chkAllowMinimize.Checked = s.AllowMinimize;
        chkFlashCaption.Checked = s.FlashCaption;
        chkFlashTaskbar.Checked = s.FlashTaskbar;
        chkShake.Checked = s.Shake;
        numShake.Value = s.ShakeMinutes;
        chkShakeSound.Checked = s.ShakeSound;
        numShake.Enabled = chkShakeSound.Enabled = chkShake.Checked;
        chkShake.CheckedChanged += (_, _) => numShake.Enabled = chkShakeSound.Enabled = chkShake.Checked;
        var excluded = new HashSet<string>(s.ExcludedActivities, StringComparer.OrdinalIgnoreCase);
        if (app.Store is not null && app.Log is not null)
            foreach (var name in app.AllActivityNames())
                (excluded.Contains(name) ? lbIgnore : lbAll).Items.Add(name);
        foreach (var name in excluded.Where(n => !lbIgnore.Items.Contains(n))) lbIgnore.Items.Add(name);

        cbLanguage.Items.Add(L.F("settings.languageAuto", L.Resolve(null).NativeName));
        foreach (var lang in L.All) cbLanguage.Items.Add(lang.NativeName);
        int li = Array.FindIndex(L.All, l => l.Code.Equals(s.Language, StringComparison.OrdinalIgnoreCase));
        cbLanguage.SelectedIndex = li + 1;
        segTheme.SelectedIndex = (int)s.Theme;
        Theme.Apply(this);
        Rtl.Apply(this);
    }

    public static string DefaultDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), L.T("data.folderName"));

    TabPage BuildGeneral()
    {
        var page = new TabPage(L.T("settings.tabGeneral")) { Padding = new Padding(10) };
        var g = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        g.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        g.Controls.Add(new Label { Text = L.T("settings.dataDir"), AutoSize = true, Margin = new Padding(3, 3, 3, 6) }, 0, 0);
        g.SetColumnSpan(g.GetControlFromPosition(0, 0)!, 2);
        var choose = new ModernButton { Text = L.T("settings.change"), AutoSize = true, MinimumSize = new Size(90, 28) };
        choose.Click += (_, _) => ChooseDir();
        g.Controls.Add(choose, 0, 1);
        lblDir.Anchor = AnchorStyles.Left;
        g.Controls.Add(lblDir, 1, 1);

        var intervalLabel = new Label { Text = L.T("settings.interval"), AutoSize = true, Margin = new Padding(3, 18, 3, 3) };
        g.Controls.Add(intervalLabel, 0, 2);
        g.SetColumnSpan(intervalLabel, 2);
        var intervalRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        numInterval.Margin = new Padding(3, 6, 3, 3);
        intervalRow.Controls.AddRange([numInterval, trackInterval]);
        g.Controls.Add(intervalRow, 0, 3);
        g.SetColumnSpan(intervalRow, 2);
        trackInterval.Scroll += (_, _) => numInterval.Value = trackInterval.Value;
        numInterval.ValueChanged += (_, _) =>
            trackInterval.Value = (int)Math.Clamp(numInterval.Value, trackInterval.Minimum, trackInterval.Maximum);

        var recentLabel = new Label { Text = L.T("settings.recent"), AutoSize = true, Margin = new Padding(3, 18, 3, 3) };
        g.Controls.Add(recentLabel, 0, 4);
        g.SetColumnSpan(recentLabel, 2);
        g.Controls.Add(numRecent, 0, 5);

        chkCommentAuto.Margin = new Padding(3, 18, 3, 3);
        g.Controls.Add(chkCommentAuto, 0, 6);
        g.SetColumnSpan(chkCommentAuto, 2);

        // Im Store-Paket regelt Windows den Autostart (Einstellungen › Apps › Autostart): dorthin verlinken.
        Control autostart = Installer.IsPackaged ? AutostartLink() : chkAutostart;
        autostart.Margin = new Padding(3, 12, 3, 3);
        g.Controls.Add(autostart, 0, 7);
        g.SetColumnSpan(autostart, 2);
        page.Controls.Add(g);

        // Darstellung und Sprache ganz oben; der Zusatz "(Language)" hilft, falls man die eingestellte Sprache nicht lesen kann.
        var top = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(0, 0, 0, 12) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var languageLabel = L.T("settings.language") + (L.Current.Code == "en" ? ":" : " (Language):");
        top.Controls.Add(new Label { Text = L.T("settings.theme") + ":", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 3, 12, 3) }, 0, 0);
        segTheme.Margin = new Padding(3, 3, 3, 8);
        top.Controls.Add(segTheme, 1, 0);
        top.Controls.Add(new Label { Text = languageLabel, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 3, 12, 3) }, 0, 1);
        top.Controls.Add(cbLanguage, 1, 1);
        page.Controls.Add(top);
        return page;
    }

    static LinkLabel AutostartLink()
    {
        var link = new LinkLabel
        {
            Text = L.T("settings.autostartStore"), AutoSize = true, MaximumSize = new Size(520, 0),
            LinkColor = Theme.Current.Accent, ActiveLinkColor = Theme.Current.Accent, LinkBehavior = LinkBehavior.HoverUnderline,
        };
        link.LinkClicked += (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true });
        return link;
    }

    TabPage BuildEvaluation()
    {
        var page = new TabPage(L.T("settings.tabEvaluation")) { Padding = new Padding(10) };
        var g = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
        g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        g.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        g.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        g.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        g.Controls.Add(new Label { Text = L.T("settings.included"), AutoSize = true }, 0, 0);
        g.Controls.Add(new Label { Text = L.T("settings.excluded"), AutoSize = true }, 2, 0);
        g.Controls.Add(lbAll, 0, 1);
        g.Controls.Add(lbIgnore, 2, 1);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Anchor = AnchorStyles.None };
        var ignore = new ModernButton { Text = L.T("settings.exclude"), AutoSize = true, MinimumSize = new Size(110, 30) };
        var include = new ModernButton { Text = L.T("settings.include"), AutoSize = true, MinimumSize = new Size(110, 30) };
        ignore.Click += (_, _) => Transfer(lbAll, lbIgnore);
        include.Click += (_, _) => Transfer(lbIgnore, lbAll);
        lbAll.DoubleClick += (_, _) => Transfer(lbAll, lbIgnore);
        lbIgnore.DoubleClick += (_, _) => Transfer(lbIgnore, lbAll);
        buttons.Controls.AddRange([ignore, include]);
        g.Controls.Add(buttons, 1, 1);
        page.Controls.Add(g);
        return page;
    }

    static void Transfer(ListBox from, ListBox to)
    {
        var items = from.SelectedItems.Cast<object>().ToList();
        foreach (var i in items)
        {
            from.Items.Remove(i);
            to.Items.Add(i);
        }
    }

    TabPage BuildHotkeys()
    {
        var page = new TabPage(L.T("settings.tabHotkeys")) { Padding = new Padding(10) };
        var g = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        g.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var blurb = new Label
        {
            Text = L.T("settings.hotkeysInfo"),
            AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(3, 3, 3, 14),
        };
        g.Controls.Add(blurb, 0, 0);
        g.SetColumnSpan(blurb, 2);
        g.Controls.Add(new Label { Text = L.T("settings.hotkeyFinish"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        g.Controls.Add(hkFinish, 1, 1);
        g.Controls.Add(new Label { Text = L.T("settings.hotkeyEdit"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        g.Controls.Add(hkEdit, 1, 2);
        page.Controls.Add(g);
        return page;
    }

    TabPage BuildReminder()
    {
        var page = new TabPage(L.T("settings.tabReminder")) { Padding = new Padding(10) };
        var g = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        g.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        g.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var blurb = new Label
        {
            Text = L.T("settings.reminderInfo"),
            AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(3, 3, 3, 14),
        };
        g.Controls.Add(blurb, 0, 0);
        g.SetColumnSpan(blurb, 3);
        g.Controls.Add(chkAllowMinimize, 0, 1);
        g.SetColumnSpan(chkAllowMinimize, 3);
        var more = new Label
        {
            Text = L.T("settings.reminderMore"),
            AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(3, 18, 3, 8),
        };
        g.Controls.Add(more, 0, 2);
        g.SetColumnSpan(more, 3);
        g.Controls.Add(chkFlashCaption, 0, 3);
        g.SetColumnSpan(chkFlashCaption, 3);
        g.Controls.Add(chkFlashTaskbar, 0, 4);
        g.SetColumnSpan(chkFlashTaskbar, 3);
        chkShake.Margin = new Padding(3, 12, 0, 3);
        numShake.Margin = new Padding(3, 10, 3, 3);
        g.Controls.Add(chkShake, 0, 5);
        g.Controls.Add(numShake, 1, 5);
        g.Controls.Add(new Label { Text = L.T("settings.minutes"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 12, 3, 3) }, 2, 5);
        chkShakeSound.Margin = new Padding(22, 3, 3, 3);
        g.Controls.Add(chkShakeSound, 0, 6);
        g.SetColumnSpan(chkShakeSound, 3);
        page.Controls.Add(g);
        return page;
    }

    void ChooseDir()
    {
        using var dlg = new FolderBrowserDialog { Description = L.T("settings.dataDirDialog"), SelectedPath = dataDir, UseDescriptionForTitle = true };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            dataDir = dlg.SelectedPath;
            lblDir.Text = dataDir;
        }
    }

    void Apply()
    {
        if (hkFinish.Hotkey != Keys.None && hkFinish.Hotkey == hkEdit.Hotkey)
        {
            Msg.Show(this, L.T("settings.hotkeysSame"));
            tabs.SelectedIndex = 2;
            return;
        }
        var s = app.Settings;
        s.DataDir = dataDir;
        s.IntervalMinutes = (int)numInterval.Value;
        s.RecentCount = (int)numRecent.Value;
        s.HotkeyFinish = hkFinish.Hotkey;
        s.HotkeyEdit = hkEdit.Hotkey;
        s.ExcludedActivities = lbIgnore.Items.Cast<string>().ToList();
        if (!Installer.IsPackaged) s.Autostart = chkAutostart.Checked;
        s.CommentOnlyWhenFilled = chkCommentAuto.Checked;
        s.Language = cbLanguage.SelectedIndex <= 0 ? null : L.All[cbLanguage.SelectedIndex - 1].Code;
        L.Use(s.Language);
        s.Theme = (ThemeMode)segTheme.SelectedIndex;
        Theme.Use(s.Theme);
        s.AllowMinimize = chkAllowMinimize.Checked;
        s.FlashCaption = chkFlashCaption.Checked;
        s.FlashTaskbar = chkFlashTaskbar.Checked;
        s.Shake = chkShake.Checked;
        s.ShakeMinutes = (int)numShake.Value;
        s.ShakeSound = chkShakeSound.Checked;
        if (!Installer.IsPackaged) SetAutostart(chkAutostart.Checked);
        s.Save();
        DialogResult = DialogResult.OK;
        Close();
    }

    static bool IsAutostartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunValue) is string;
    }

    static void SetAutostart(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
        else if (key.GetValue(RunValue) is not null) key.DeleteValue(RunValue);
    }
}

/// <summary>Eingabefeld für eine Tastenkombination.</summary>
public sealed class HotkeyBox : TextBox
{
    Keys hotkey;

    public HotkeyBox()
    {
        ReadOnly = true;
        BackColor = SystemColors.Window;
        ShortcutsEnabled = false;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Keys Hotkey
    {
        get => hotkey;
        set { hotkey = value; Text = Describe(value); }
    }

    public static string Describe(Keys k)
    {
        if (k == Keys.None) return L.T("hotkey.none");
        var parts = new List<string>();
        if (k.HasFlag(Keys.Control)) parts.Add(L.T("key.ctrl"));
        if (k.HasFlag(Keys.Alt)) parts.Add("Alt");
        if (k.HasFlag(Keys.Shift)) parts.Add(L.T("key.shift"));
        parts.Add(new KeysConverter().ConvertToString(k & Keys.KeyCode) ?? (k & Keys.KeyCode).ToString());
        return string.Join("+", parts);
    }

    // Tab, Enter und Esc ohne Zusatztaste behalten ihre normale Funktion im Dialog.
    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.Modifiers) != 0 || (keyData & Keys.KeyCode) is not (Keys.Tab or Keys.Enter or Keys.Escape);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = e.SuppressKeyPress = true;
        var code = e.KeyCode;
        if (code is Keys.Back or Keys.Delete && e.Modifiers == Keys.None) { Hotkey = Keys.None; return; }
        if (code == Keys.ShiftKey && e.Modifiers == Keys.Shift) { Hotkey = Keys.None; return; }
        if (code is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return;
        // Ohne Strg/Alt nur Funktionstasten erlauben, sonst wäre normales Tippen blockiert.
        bool fkey = code is >= Keys.F1 and <= Keys.F24;
        if (!fkey && (e.Modifiers & (Keys.Control | Keys.Alt)) == 0) return;
        Hotkey = code | e.Modifiers;
    }

    protected override void OnKeyPress(KeyPressEventArgs e) => e.Handled = true;
}
