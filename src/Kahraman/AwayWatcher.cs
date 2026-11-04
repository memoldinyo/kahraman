using System.Runtime.InteropServices;
using Kahraman.Core;
using Microsoft.Win32;

namespace Kahraman;

/// <summary>
/// Merkt, wann niemand am Rechner ist (Sperren, Abmelden, Herunterfahren, Energiesparmodus, keine Eingabe),
/// und meldet die Rückkehr. Der Zustand steht in den Einstellungen und übersteht so ein Herunterfahren.
/// Was davon bemerkt wird, ist einstellbar (Einstellungen › Abwesenheit); Pause und Feierabend über das Menü gehen immer.
/// </summary>
sealed class AwayWatcher : IDisposable
{
    readonly Settings settings;
    readonly SynchronizationContext context;
    readonly object gate = new();
    bool locked, breakEndSeen;

    /// <summary>Jemand ist zurück (Zeitpunkt). Bei einer Pause, deren Ende man am Rechner erlebt hat, mit fester Buchung.</summary>
    public event Action<DateTime, PauseChoice?>? Returned;
    /// <summary>Weg oder wieder da: Symbol und Menü auffrischen.</summary>
    public event Action? Changed;

    public AwayWatcher(Settings settings)
    {
        this.settings = settings;
        context = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        SystemEvents.SessionSwitch += SessionSwitch;
        SystemEvents.SessionEnding += SessionEnding;
        SystemEvents.PowerModeChanged += PowerModeChanged;
    }

    public AwayState? State => settings.Away;

    /// <summary>Gehen merken. Ist schon jemand weg, bleibt der frühere Zeitpunkt; eine selbst gewählte Pause bleibt, wie sie ist.</summary>
    public void Leave(DateTime since, AwayKind kind, DateTime? plannedEnd = null, bool workLogged = false)
    {
        lock (gate)
        {
            if (settings.Away is { } current && (current.Manual || current.Since <= since)) return;
            settings.Away = new AwayState(since.AddTicks(-(since.Ticks % TimeSpan.TicksPerSecond)), kind, plannedEnd, workLogged);
            breakEndSeen = false;
            settings.Save();
        }
        Changed?.Invoke();
    }

    public void Clear()
    {
        lock (gate)
        {
            if (settings.Away is null) return;
            settings.Away = null;
            settings.Save();
        }
        Changed?.Invoke();
    }

    /// <summary>Alle paar Sekunden: Inaktivität bemerken, Ende einer Pause, Rückkehr.</summary>
    public void Tick(DateTime now)
    {
        var idle = IdleTime();
        var lastInput = now - idle;
        if (settings.Away is not { } away)
        {
            if (settings.AwayOnIdle && idle >= TimeSpan.FromMinutes(settings.AwayIdleMinutes)) Leave(lastInput, AwayKind.Idle);
            return;
        }
        if (locked) return;
        bool active = idle < TimeSpan.FromSeconds(15);
        switch (away.Kind)
        {
            case AwayKind.Break:
                // Pause mit fester Dauer: wer bei ihrem Ende am Rechner ist, bekommt sie ohne Rückfrage gebucht,
                // wer später kommt, wird gefragt ("geplant" oder "bis jetzt").
                if (away.PlannedEnd is not { } end || now < end) return;
                if (!breakEndSeen)
                {
                    breakEndSeen = true;
                    // Nur wenn Kahraman das Ende gerade selbst erlebt (nicht erst nach Neustart oder Energiesparmodus).
                    bool atEnd = now - end < TimeSpan.FromMinutes(1);
                    if (atEnd && idle < TimeSpan.FromMinutes(2)) { Returned?.Invoke(now, PauseChoice.UntilPlanned); return; }
                }
                if (active && lastInput > end) Returned?.Invoke(now, null);
                return;
            case AwayKind.EndOfDay:
                if (active && now.Date > away.Since.Date) Returned?.Invoke(now, null);
                return;
            default:
                if (active && lastInput > away.Since.AddSeconds(5)) Returned?.Invoke(now, null);
                return;
        }
    }

    void SessionSwitch(object? sender, SessionSwitchEventArgs e) => context.Post(_ =>
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect:
                locked = true;
                if (settings.AwayOnLock) Leave(LastInput(), AwayKind.Lock);
                break;
            case SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect:
                locked = false;
                break;
        }
    }, null);

    // Beim Abmelden/Herunterfahren bleibt keine Zeit für Umwege über die Nachrichtenschleife: sofort speichern.
    void SessionEnding(object? sender, SessionEndingEventArgs e)
    {
        if (e.Reason == SessionEndReasons.Logoff ? settings.AwayOnSignOut : settings.AwayOnShutdown)
            Leave(LastInput(), e.Reason == SessionEndReasons.Logoff ? AwayKind.SignOut : AwayKind.Shutdown);
    }

    void PowerModeChanged(object? sender, PowerModeChangedEventArgs e) => context.Post(_ =>
    {
        if (e.Mode == PowerModes.Suspend && settings.AwayOnSleep) Leave(LastInput(), AwayKind.Sleep);
    }, null);

    static DateTime LastInput() => DateTime.Now - IdleTime();

    /// <summary>Zeit seit der letzten Maus- oder Tastatureingabe in dieser Sitzung.</summary>
    static TimeSpan IdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
    }

    [StructLayout(LayoutKind.Sequential)]
    struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= SessionSwitch;
        SystemEvents.SessionEnding -= SessionEnding;
        SystemEvents.PowerModeChanged -= PowerModeChanged;
    }
}
