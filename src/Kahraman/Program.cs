using System.Security.Cryptography;
using System.Text;
using Kahraman.Core;
using Kahraman.Ui;

namespace Kahraman;

public enum StartCommand { Eingabe, Logfile, Auswertung, Einstellungen, Beenden }

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // --settings <datei> erlaubt einen zweiten, unabhängigen Kahraman (z. B. zum Ausprobieren mit Testdaten).
        var settingsArg = ArgValue(args, "--settings");
        var settingsFile = Path.GetFullPath(settingsArg ?? Settings.DefaultFile);
        L.Use(Settings.Load(settingsFile).Language);

        if (args.Contains("--uninstall"))
        {
            if (!Installer.IsPackaged) Installer.Uninstall();
            return;
        }

        var command = args.Contains("--logfile") ? StartCommand.Logfile
            : args.Contains("--auswertung") ? StartCommand.Auswertung
            : args.Contains("--einstellungen") ? StartCommand.Einstellungen
            : args.Contains("--beenden") ? StartCommand.Beenden
            : StartCommand.Eingabe;

        // Heruntergeladene Datei: Installation anbieten (außer im portablen oder Test-Betrieb und im Store-Paket).
        if (settingsArg is null && command != StartCommand.Beenden && !args.Contains("--portable") && !Installer.IsPackaged
            && !Installer.IsInstalledCopy && !Installer.CurrentExeIsRunning()
            && Installer.OfferInstall())
            return;

        using var mutex = new Mutex(true, MutexName(settingsFile), out bool first);
        var signals = Enum.GetValues<StartCommand>()
            .Select(c => new EventWaitHandle(false, EventResetMode.AutoReset, SignalName(c, settingsFile)))
            .ToArray();

        if (!first)
        {
            // Kahraman läuft bereits: dort das gewünschte Fenster öffnen (ohne Argument: "Tätigkeit abschließen").
            AllowSetForegroundWindow(-1);
            signals[(int)command].Set();
            return;
        }
        if (command == StartCommand.Beenden) return;

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ShowError(e.ExceptionObject as Exception);

        Application.Run(new TrayApp(Settings.Load(settingsFile), signals, command));
        GC.KeepAlive(mutex);
    }

    static string Hash(string settingsFile) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(settingsFile).ToLowerInvariant())))[..16];

    static string MutexName(string settingsFile) => @"Local\Kahraman-" + Hash(settingsFile);

    static string SignalName(StartCommand command, string settingsFile) => $@"Local\Kahraman-{command}-{Hash(settingsFile)}";

    /// <summary>Signalisiert einer laufenden Instanz einen Befehl. Liefert false, wenn keine läuft.</summary>
    public static bool Signal(StartCommand command, string settingsFile)
    {
        if (!EventWaitHandle.TryOpenExisting(SignalName(command, settingsFile), out var handle)) return false;
        using (handle) handle.Set();
        return true;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int processId);

    static string? ArgValue(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    static void ShowError(Exception? ex)
    {
        var text = ex?.ToString() ?? "?";
        try
        {
            var log = Path.Combine(Path.GetDirectoryName(Settings.DefaultFile)!, "fehler.log");
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.AppendAllText(log, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n{text}\r\n\r\n");
        }
        catch (IOException) { }
        Msg.Error(null, L.T("error.unexpected") + "\n\n" + ex?.Message);
    }
}
