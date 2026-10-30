using System.Diagnostics;
using System.Runtime.InteropServices;
using Kahraman.Core;
using Kahraman.Ui;
using Microsoft.Win32;

namespace Kahraman;

/// <summary>
/// Einfache Installation ohne Administratorrechte: Programmordner im Benutzerprofil, Startmenü-Eintrag,
/// Autostart und Eintrag unter "Apps" zum Deinstallieren. Die Kahraman.exe ist damit zugleich ihr eigenes Setup.
/// </summary>
static class Installer
{
    const string AppName = "Kahraman";
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Kahraman";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static string InstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);
    public static string InstalledExe => Path.Combine(InstallDir, "Kahraman.exe");
    static string ShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Kahraman.lnk");
    static string CurrentExe => Environment.ProcessPath!;
    static string CurrentVersion => typeof(Installer).Assembly.GetName().Version?.ToString(3) ?? "?";

    static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    public static bool IsInstalledCopy => SamePath(CurrentExe, InstalledExe);

    /// <summary>
    /// Läuft Kahraman als Paket aus dem Microsoft Store? Dann übernimmt Windows Installation, Autostart,
    /// Updates und Deinstallation – die eigene Installation bleibt aus.
    /// </summary>
    public static bool IsPackaged { get; } = DetectPackage();

    static bool DetectPackage()
    {
        const int AppModelErrorNoPackage = 15700;
        int length = 0;
        try { return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage; }
        catch (EntryPointNotFoundException) { return false; }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

    /// <summary>Läuft diese Datei bereits (als anderer Prozess)?</summary>
    public static bool CurrentExeIsRunning() => RunningInstances(CurrentExe).Count > 0;

    static List<Process> RunningInstances(string exe)
    {
        var me = Environment.ProcessId;
        return Process.GetProcessesByName(AppName).Where(p =>
        {
            if (p.Id == me) return false;
            try { return p.MainModule?.FileName is { } f && SamePath(f, exe); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
        }).ToList();
    }

    /// <summary>Bietet die Installation an. Liefert true, wenn dieser Prozess danach beendet werden soll.</summary>
    public static bool OfferInstall()
    {
        string? installedVersion = File.Exists(InstalledExe) ? FileVersionInfo.GetVersionInfo(InstalledExe).ProductVersion : null;
        var text = installedVersion is null
            ? L.F("install.offer", InstallDir)
            : L.F("install.update", installedVersion.Split('+')[0], CurrentVersion);
        var answer = Msg.Show(null, text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (answer == DialogResult.Cancel) return true;
        if (answer == DialogResult.No) return false;
        if (installedVersion is null && !AcceptLicense()) return true;
        try
        {
            Install();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            Msg.Error(null, L.T("install.failed") + "\n\n" + ex.Message);
            return false;
        }
        Process.Start(new ProcessStartInfo(InstalledExe) { UseShellExecute = true, WorkingDirectory = InstallDir });
        return true;
    }

    /// <summary>Zeigt die Lizenzbedingungen vor der ersten Installation und fragt nach Zustimmung.</summary>
    static bool AcceptLicense()
    {
        using var form = new Form
        {
            Text = L.T("license.title"),
            Icon = Ui.IconFactory.AppIcon,
            Font = Ui.Theme.Font,
            Size = new Size(700, 560),
            MinimumSize = new Size(450, 300),
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
        };
        var box = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            BackColor = SystemColors.Window, Text = HelpText.LicenseText.ReplaceLineEndings("\r\n"),
            Font = new Font("Consolas", 9.5f),
        };
        box.HandleCreated += (_, _) => box.SelectionStart = 0;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(6) };
        var decline = new Ui.ModernButton { Text = L.T("license.decline"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(110, 30) };
        var accept = new Ui.ModernButton { Text = L.T("license.accept"), DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(110, 30) };
        buttons.Controls.AddRange([decline, accept]);
        form.Controls.Add(box);
        form.Controls.Add(buttons);
        form.AcceptButton = accept;
        form.CancelButton = decline;
        Ui.Theme.Apply(form);
        Rtl.Apply(form);
        return form.ShowDialog() == DialogResult.OK;
    }

    static void Install()
    {
        StopRunning(InstalledExe);
        Directory.CreateDirectory(InstallDir);
        File.Copy(CurrentExe, InstalledExe, overwrite: true);
        CreateShortcut(ShortcutPath, InstalledExe, L.T("app.tagline"));
        using (var run = Registry.CurrentUser.CreateSubKey(RunKey))
            run.SetValue(AppName, $"\"{InstalledExe}\"");
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
        key.SetValue("DisplayName", L.T("app.productName"));
        key.SetValue("DisplayVersion", CurrentVersion);
        key.SetValue("Publisher", AppName);
        key.SetValue("DisplayIcon", InstalledExe);
        key.SetValue("InstallLocation", InstallDir);
        key.SetValue("UninstallString", $"\"{InstalledExe}\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024), RegistryValueKind.DWord);
    }

    public static void Uninstall()
    {
        if (Msg.Show(null, L.T("uninstall.confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        StopRunning(InstalledExe);
        if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
        using (var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
            if (run?.GetValue(AppName) is string v && v.Contains(InstallDir, StringComparison.OrdinalIgnoreCase))
                run.DeleteValue(AppName);
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);
        Msg.Show(null, L.T("uninstall.done"));

        // Die laufende Programmdatei kann sich nicht selbst löschen: das übernimmt cmd.exe,
        // sobald dieser Prozess beendet ist (direkt nach diesem Aufruf).
        Process.Start(new ProcessStartInfo("cmd.exe",
            $"/c ping 127.0.0.1 -n 4 > nul & del /f /q \"{InstalledExe}\" & rmdir \"{InstallDir}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetTempPath(),
        });
    }

    /// <summary>Beendet eine laufende Kahraman-Instanz dieser Programmdatei (erst höflich, dann hart).</summary>
    static void StopRunning(string exe)
    {
        var running = RunningInstances(exe);
        if (running.Count == 0) return;
        Program.Signal(StartCommand.Beenden, Settings.DefaultFile);
        foreach (var p in running)
        {
            if (p.WaitForExit(5000)) continue;
            p.Kill();
            p.WaitForExit(3000);
        }
    }

    static void CreateShortcut(string path, string target, string description)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new COMException("WScript.Shell");
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic link = shell.CreateShortcut(path);
            link.TargetPath = target;
            link.WorkingDirectory = Path.GetDirectoryName(target);
            link.Description = description;
            link.IconLocation = target + ",0";
            link.Save();
            Marshal.FinalReleaseComObject(link);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }
}
