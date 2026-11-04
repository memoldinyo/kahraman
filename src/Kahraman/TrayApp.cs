using Kahraman.Core;
using Kahraman.Ui;

namespace Kahraman;

/// <summary>Kahraman im Infobereich: Zifferblatt, Menü, Abfrage-Timer und Hotkeys.</summary>
public sealed class TrayApp : ApplicationContext
{
    const int HotkeyFinishId = 1, HotkeyEditId = 2;

    readonly AppServices app;
    readonly NotifyIcon tray;
    readonly HotkeyWindow hotkeys = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 5000 };
    InputForm? input;
    LogEditorForm? editor;
    EvaluationForm? evaluation;
    DateTime nextPrompt = DateTime.MinValue; // beim Start sofort fragen
    DateTime lastIconUpdate;
    DateTime lastRefresh;
    bool ready, initializing;
    readonly StartCommand startCommand;
    readonly SynchronizationContext ui;
    readonly AwayWatcher away;
    // Fragen pausiert (Menü), DateTime.MaxValue = bis "Fragen fortsetzen".
    DateTime? questionsPausedUntil;
    // Nach dem nächsten Eintrag: Pause oder Feierabend beginnen.
    Action<DateTime>? afterEntry;
    bool returning;

    public TrayApp(Settings settings, WaitHandle[] signals, StartCommand startCommand)
    {
        this.startCommand = startCommand;
        // Ein zweiter Programmstart signalisiert, welches Fenster geöffnet werden soll.
        var context = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        ui = context;
        foreach (var command in Enum.GetValues<StartCommand>())
            ThreadPool.RegisterWaitForSingleObject(signals[(int)command],
                (_, _) => context.Post(_ => { if (ready) Run(command); }, null), null, -1, false);

        app = new AppServices(settings);
        away = new AwayWatcher(settings);
        away.Changed += () => ui.Post(_ => UpdateTray(), null);
        away.Returned += OnReturned;
        HelpText.Settings = settings;
        tray = new NotifyIcon { Text = L.T("tray.starting"), Visible = true, ContextMenuStrip = BuildMenu() };
        UpdateIcon();
        tray.MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left && ready) FinishActivity(); };
        hotkeys.Pressed += id =>
        {
            if (!ready) return;
            if (id == HotkeyFinishId) FinishActivity();
            else if (id == HotkeyEditId) EditLog();
        };
        app.DataChanged += UpdateTray;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += WindowsPreferenceChanged;
        timer.Tick += (_, _) => Tick();
        timer.Interval = 300; // erster Tick: Initialisierung
        timer.Start();
    }

    ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        var finish = new ToolStripMenuItem(L.T("tray.finish"), null, (_, _) => FinishActivity()) { Font = new Font(menu.Font, FontStyle.Bold) };
        menu.Items.Add(finish);
        // Pausieren: Fragen aussetzen (Zeit zählt weiter), Pause (wird als Pause gebucht), Feierabend.
        var pauseQuestions = Durations(L.T("tray.pauseQuestions"), PauseQuestions);
        var resumeQuestions = new ToolStripMenuItem(L.T("tray.resumeQuestions"), null, (_, _) => ResumeQuestions());
        var takeBreak = Durations(L.T("tray.break"), StartBreak);
        var endBreak = new ToolStripMenuItem(L.T("tray.endBreak"), null, (_, _) => EndBreak());
        var endOfDay = new ToolStripMenuItem(L.T("tray.endOfDay"), null, (_, _) => EndOfDay());
        var resumeWork = new ToolStripMenuItem(L.T("tray.resumeWork"), null, (_, _) => ResumeWork());
        menu.Items.AddRange([pauseQuestions, resumeQuestions, takeBreak, endBreak, endOfDay, resumeWork, new ToolStripSeparator()]);
        menu.Items.Add(L.T("tray.edit"), null, (_, _) => EditLog());
        menu.Items.Add(L.T("tray.import"), null, (_, _) => ImportLog());
        menu.Items.Add(L.T("tray.importSubs"), null, (_, _) => ImportSubs());
        menu.Items.Add(L.T("tray.evaluation"), null, (_, _) => ShowEvaluation());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.T("tray.settings"), null, (_, _) => OpenSettings(null, 0));
        menu.Items.Add(L.T("tray.about"), null, (_, _) => HelpText.About(null, app));
        // Lizenz, Hilfe und Beenden gehen auch, solange Kahraman noch startet.
        menu.Items.Add(new ToolStripMenuItem(L.T("tray.license"), null, (_, _) => HelpText.License(null)) { Tag = true });
        menu.Items.Add(new ToolStripMenuItem(L.T("tray.help"), null, (_, _) => HelpText.Show(null)) { Tag = true });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(L.T("tray.quit"), null, (_, _) => Quit()) { Tag = true });
        menu.Opening += (_, e) =>
        {
            foreach (ToolStripItem item in menu.Items)
                item.Enabled = ready || item.Tag is true;
            bool paused = questionsPausedUntil is not null;
            var kind = app.Settings.Away?.Kind;
            bool onBreak = kind == AwayKind.Break, offWork = kind == AwayKind.EndOfDay;
            pauseQuestions.Visible = !paused;
            resumeQuestions.Visible = paused;
            takeBreak.Visible = endOfDay.Visible = !onBreak && !offWork;
            endBreak.Visible = onBreak;
            resumeWork.Visible = offWork;
        };
        Rtl.Apply(menu);
        return menu;
    }

    static ToolStripMenuItem Durations(string text, Action<TimeSpan?> pick)
    {
        var item = new ToolStripMenuItem(text);
        foreach (var (key, minutes) in new[] { ("tray.minutes30", 30), ("tray.hour1", 60), ("tray.hours2", 120), ("tray.untilResume", 0) })
            item.DropDownItems.Add(L.T(key), null, (_, _) => pick(minutes == 0 ? null : TimeSpan.FromMinutes(minutes)));
        return item;
    }

    /// <summary>Andere Sprache eingestellt: Menü und Infotext neu aufbauen (offene Fenster behalten ihre Sprache).</summary>
    void LanguageChanged()
    {
        var old = tray.ContextMenuStrip;
        tray.ContextMenuStrip = BuildMenu();
        old?.Dispose();
        UpdateTray();
    }

    /// <summary>Hell/Dunkel in Windows umgestellt: im Modus "System" gilt das für alle danach geöffneten Fenster.</summary>
    void WindowsPreferenceChanged(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category == Microsoft.Win32.UserPreferenceCategory.General) Theme.Refresh();
    }

    void Tick()
    {
        if (!ready)
        {
            if (!initializing) Initialize();
            return;
        }
        var now = DateTime.Now;
        if (now - lastRefresh > TimeSpan.FromMinutes(1))
        {
            lastRefresh = now;
            Safe(app.Refresh);
        }
        if (!returning) away.Tick(now);
        if (questionsPausedUntil is { } until && now >= until)
        {
            questionsPausedUntil = null;
            UpdateTray();
        }
        if (input is null && now >= nextPrompt && !QuestionsSuspended) FinishActivity();
        if (now.Minute != lastIconUpdate.Minute || now - lastIconUpdate > TimeSpan.FromMinutes(1)) UpdateTray();
    }

    void Initialize()
    {
        initializing = true;
        timer.Stop();
        // Hell/Dunkel erst hier einschalten, wenn die Nachrichtenschleife läuft: vor Application.Run eingeschaltet,
        // blieb Kahraman später beim Öffnen der Auswertung hängen.
        Theme.Use(app.Settings.Theme);
        try
        {
            var s = app.Settings;
            if (string.IsNullOrEmpty(s.DataDir))
            {
                var answer = Msg.Show(null, L.F("tray.welcome", SettingsForm.DefaultDataDir), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel) { Quit(); return; }
                if (answer == DialogResult.Yes) s.DataDir = SettingsForm.DefaultDataDir;
            }
            while (true)
            {
                bool opened;
                try { opened = app.Open(null); }
                catch (Exception ex) when (ex is KahramanException or IOException or UnauthorizedAccessException)
                {
                    Msg.Error(null, ex.Message);
                    opened = false;
                }
                if (opened) break;
                using var f = new SettingsForm(app);
                if (f.ShowDialog() != DialogResult.OK) { Quit(); return; }
            }
            s.Save();
            RegisterHotkeys(showErrors: true);
            ready = true;
            lastRefresh = DateTime.Now;
            UpdateTray();
            timer.Interval = 5000;
            // Nach Feierabend, Herunterfahren usw. kommt zuerst die Rückfrage (beim nächsten Tick), nicht die Abfrage.
            if (app.Settings.Away is null) FinishActivity();
            if (startCommand != StartCommand.Eingabe) Run(startCommand);
        }
        finally
        {
            initializing = false;
            timer.Interval = 5000;
            if (tray.Visible) timer.Start();
        }
    }

    void RegisterHotkeys(bool showErrors)
    {
        var failed = new List<string>();
        if (!hotkeys.Register(HotkeyFinishId, app.Settings.HotkeyFinish)) failed.Add(HotkeyBox.Describe(app.Settings.HotkeyFinish));
        if (!hotkeys.Register(HotkeyEditId, app.Settings.HotkeyEdit)) failed.Add(HotkeyBox.Describe(app.Settings.HotkeyEdit));
        if (failed.Count > 0 && showErrors)
            Msg.Show(null, L.F("tray.hotkeysTaken", string.Join(", ", failed)), MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    void Run(StartCommand command)
    {
        switch (command)
        {
            case StartCommand.Logfile: EditLog(); break;
            case StartCommand.Auswertung: ShowEvaluation(); break;
            case StartCommand.Einstellungen: OpenSettings(null, 0); break;
            case StartCommand.Beenden: Quit(); break;
            default: FinishActivity(); break;
        }
    }

    /// <summary>Öffnet das Eingabefenster (fällige Abfrage, Menü, Doppelklick oder Hotkey).</summary>
    void FinishActivity(string? question = null)
    {
        if (input is not null) { input.BringUp(); return; }
        Safe(app.Refresh);
        var form = new InputForm(app, InputForm.InputMode.Popup, LastWork(), question: question);
        form.FormClosed += (_, _) =>
        {
            var interval = TimeSpan.FromMinutes(app.Settings.IntervalMinutes);
            var entry = form.DialogResult == DialogResult.OK ? form.Result : null;
            nextPrompt = (entry?.Time ?? DateTime.Now) + interval;
            input = null;
            var then = afterEntry;
            afterEntry = null;
            if (entry is not null)
            {
                // Ein Eintrag deckt die Zeit bis jetzt ab: eine bemerkte Abwesenheit oder Pause ist damit erledigt.
                away.Clear();
                then?.Invoke(entry.Time);
            }
            UpdateTray();
            form.Dispose();
        };
        input = form;
        UpdateTray();
        form.Show();
    }

    /// <summary>Erst eintragen, was bis jetzt war, dann <paramref name="then"/> (Pause oder Feierabend beginnen).</summary>
    void LogThen(string question, Action<DateTime> then)
    {
        afterEntry = then;
        FinishActivity(question);
    }

    /// <summary>Vorschlag für die Abfrage: der letzte Eintrag, aber nicht "Pause" oder "Start" – nach der Pause geht es meist
    /// mit der Arbeit davor weiter.</summary>
    LogEntry? LastWork()
    {
        var entries = app.Log.Entries;
        for (int i = entries.Count - 1; i >= Math.Max(0, entries.Count - 50); i--)
        {
            var a = entries[i].Activity.Trim();
            if (!a.Equals(BreakActivity, StringComparison.OrdinalIgnoreCase) && !a.Equals(L.T("away.startActivity"), StringComparison.OrdinalIgnoreCase))
                return entries[i];
        }
        return app.Log.Last;
    }

    bool QuestionsSuspended => questionsPausedUntil is not null || app.Settings.Away is not null;

    string BreakActivity => string.IsNullOrWhiteSpace(app.Settings.BreakActivity) ? L.T("away.breakActivity") : app.Settings.BreakActivity.Trim();

    void PauseQuestions(TimeSpan? duration)
    {
        questionsPausedUntil = duration is { } d ? DateTime.Now + d : DateTime.MaxValue;
        UpdateTray();
    }

    void ResumeQuestions()
    {
        questionsPausedUntil = null;
        UpdateTray();
    }

    void StartBreak(TimeSpan? duration) =>
        LogThen(L.T("tray.breakQuestion"), t => away.Leave(t, AwayKind.Break, duration is { } d ? t + d : null, workLogged: true));

    /// <summary>Pause beenden: bis jetzt buchen; ist das geplante Ende schon vorbei, nachfragen.</summary>
    void EndBreak()
    {
        if (app.Settings.Away is not { Kind: AwayKind.Break } state) return;
        var now = DateTime.Now;
        OnReturned(now, Away.PlannedEndPassed(state, now) ? null : PauseChoice.UntilBack);
    }

    void EndOfDay() => LogThen(L.T("tray.endOfDayQuestion"), t => away.Leave(t, AwayKind.EndOfDay, workLogged: true));

    /// <summary>Doch noch einmal arbeiten am selben Abend: die Zeit seit dem Feierabend als Pause buchen oder nicht.</summary>
    void ResumeWork() => OnReturned(DateTime.Now, null);

    /// <summary>
    /// Zurück nach einer Abwesenheit: fragen, wie die Zeit gebucht wird (oder bei <paramref name="auto"/> ohne Rückfrage),
    /// die Einträge anhängen und die Abfrage danach neu takten. Eine offene Abfrage erübrigt sich damit.
    /// </summary>
    void OnReturned(DateTime back, PauseChoice? auto)
    {
        if (returning || !ready || app.Settings.Away is not { } state) return;
        if (!Away.Relevant(state, back, TimeSpan.FromMinutes(app.Settings.AwayMinMinutes)))
        {
            away.Clear();
            return;
        }
        returning = true;
        try
        {
            Safe(app.Refresh);
            var last = app.Log.Last;
            var start = L.T("away.startActivity");
            List<LogEntry> entries;
            if (auto is { } choice) entries = Away.Entries(state, last, back, null, choice, BreakActivity, start);
            else if (!Away.HasQuestion(state, last, back)) entries = [];
            else
            {
                using var form = new AwayForm(app, state, back, BreakActivity, start);
                // Mit der offenen Abfrage als Besitzer: die drängelt dann nicht dazwischen.
                if ((input is null ? form.ShowDialog() : form.ShowDialog(input)) != DialogResult.OK)
                {
                    away.Clear();
                    return;
                }
                entries = form.Entries;
            }
            Safe(() => { foreach (var e in entries) app.Record(e); });
            away.Clear();
            input?.CloseByProgram();
            nextPrompt = DateTime.Now + TimeSpan.FromMinutes(app.Settings.IntervalMinutes);
        }
        finally
        {
            returning = false;
            UpdateTray();
        }
    }

    void EditLog()
    {
        if (editor is not null) { editor.Activate(); return; }
        Safe(() =>
        {
            editor = new LogEditorForm(app);
            editor.FormClosed += (_, _) => { editor?.Dispose(); editor = null; };
            editor.Show();
            editor.Activate();
        });
    }

    void ShowEvaluation()
    {
        if (evaluation is not null) { evaluation.Activate(); return; }
        Safe(() =>
        {
            evaluation = new EvaluationForm(app, OpenSettings);
            evaluation.FormClosed += (_, _) => { evaluation?.Dispose(); evaluation = null; };
            evaluation.Show();
            evaluation.Activate();
        });
    }

    bool OpenSettings(IWin32Window? owner, int tab)
    {
        var oldDir = app.Settings.DataDir;
        var oldLanguage = L.Current;
        using var f = new SettingsForm(app, tab);
        if ((owner is null ? f.ShowDialog() : f.ShowDialog(owner)) != DialogResult.OK) return false;
        if (L.Current != oldLanguage) LanguageChanged();

        if (!string.Equals(oldDir, app.Settings.DataDir, StringComparison.OrdinalIgnoreCase))
        {
            bool opened;
            try { opened = app.Open(owner); }
            catch (Exception ex) when (ex is KahramanException or IOException or UnauthorizedAccessException)
            {
                Msg.Error(owner, ex.Message);
                opened = false;
            }
            if (!opened)
            {
                app.Settings.DataDir = oldDir;
                app.Settings.Save();
                Safe(() => app.Open(owner));
                Msg.Show(owner, L.T("tray.dirUnchanged"));
            }
        }
        RegisterHotkeys(showErrors: true);
        if (input is null)
            nextPrompt = (app.Log.Last?.Time ?? DateTime.Now) + TimeSpan.FromMinutes(app.Settings.IntervalMinutes);
        UpdateTray();
        return true;
    }

    void ImportLog()
    {
        using var dlg = new OpenFileDialog { Filter = L.T("import.filter") + " (*.kahraman)|*.kahraman", Title = L.T("import.title") };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        Safe(() =>
        {
            if (string.Equals(Path.GetFullPath(dlg.FileName), Path.GetFullPath(app.LogPath), StringComparison.OrdinalIgnoreCase))
                throw new KahramanException(L.T("import.self"));
            var other = LogFile.Load(dlg.FileName);
            if (other.Entries.Count == 0) throw new KahramanException(L.T("import.empty"));
            if (other.BadLines.Count > 0 &&
                Msg.Show(null, L.F("import.badLines", other.BadLines.Count), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            app.Log.ReloadIfChanged();
            var merged = LogFile.Merge(app.Log.Entries, other.Entries, out var conflicts);
            if (conflicts.Count > 0)
                throw new KahramanException(L.F("import.conflicts", conflicts.Count, conflicts[0].Time, conflicts[0].Activity));
            if (Msg.Show(null, L.F("import.confirm", other.Entries.Count, other.Entries[0].Time, other.Entries[^1].Time, app.LogPath),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            app.SaveLog(merged);
            Msg.Show(null, L.F("import.done", other.Entries.Count));
        });
    }

    void ImportSubs()
    {
        using var dlg = new OpenFileDialog { Filter = L.T("file.filterText"), Title = L.T("subsImport.title") };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        Safe(() =>
        {
            var (name, subs) = SubImport.Parse(TextCodec.ReadAllTextGuess(dlg.FileName));
            app.Refresh();
            var candidates = SubImport.Candidates(app.Store, name);
            string target;
            if (candidates.Count == 1) target = candidates[0].Text;
            else if (candidates.Count == 0)
            {
                if (Msg.Show(null, L.F("subsImport.notFound", name), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                target = name;
            }
            else
            {
                throw new KahramanException(L.F("subsImport.ambiguous", name, string.Join("\n", candidates.Take(15).Select(c => "• " + c.Text))));
            }
            int added = 0;
            app.ModifyStore(s =>
            {
                s.Add(target);
                foreach (var sub in subs)
                {
                    if (s.Find(target)!.FindSub(sub) is not null) continue;
                    s.AddSub(target, sub);
                    added++;
                }
            });
            Msg.Show(null, L.F("subsImport.done", added, target));
        });
    }

    void UpdateTray()
    {
        UpdateIcon();
        if (!ready || app.Log is null) return;
        var last = app.Log.Last;
        var text = last is null
            ? L.T("tray.noEntry")
            : $"Kahraman – {last.Time:t} {last.Activity.Trim()}";
        var status = app.Settings.Away switch
        {
            { Kind: AwayKind.Break, PlannedEnd: { } end } => L.F("tray.breakUntil", end),
            { Kind: AwayKind.Break } => L.T("tray.onBreak"),
            { Kind: AwayKind.EndOfDay } => L.T("tray.offWork"),
            _ => questionsPausedUntil switch
            {
                null => null,
                { } u when u == DateTime.MaxValue => L.T("tray.questionsPaused"),
                { } u => L.F("tray.questionsPausedUntil", u),
            },
        };
        if (status is not null) text += "\n" + status;
        else if (input is null && nextPrompt > DateTime.Now) text += "\n" + L.F("tray.nextQuestion", nextPrompt);
        tray.Text = text.Length > 127 ? text[..127] : text;
    }

    void UpdateIcon()
    {
        lastIconUpdate = DateTime.Now;
        var old = tray.Icon;
        int size = SystemInformation.SmallIconSize.Width;
        bool paused = questionsPausedUntil is not null || app.Settings.Away is { Manual: true };
        tray.Icon = IconFactory.Clock(size, DateTime.Now, pending: input is not null, paused: paused);
        old?.Dispose();
    }

    static void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is KahramanException or IOException or UnauthorizedAccessException)
        {
            Msg.Error(null, ex.Message);
        }
    }

    void Quit()
    {
        timer.Stop();
        input?.CloseByProgram();
        tray.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= WindowsPreferenceChanged;
            timer.Dispose();
            away.Dispose();
            hotkeys.Dispose();
            tray.Dispose();
        }
        base.Dispose(disposing);
    }
}
