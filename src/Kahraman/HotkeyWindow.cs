using System.Runtime.InteropServices;

namespace Kahraman;

/// <summary>Unsichtbares Fenster, das systemweite Hotkeys empfängt.</summary>
public sealed class HotkeyWindow : NativeWindow, IDisposable
{
    const int WM_HOTKEY = 0x0312;
    const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;
    readonly HashSet<int> registered = [];

    public event Action<int>? Pressed;

    public HotkeyWindow() => CreateHandle(new CreateParams { Parent = new IntPtr(-3) }); // HWND_MESSAGE

    public bool Register(int id, Keys keys)
    {
        Unregister(id);
        if (keys == Keys.None) return true;
        uint mod = MOD_NOREPEAT;
        if (keys.HasFlag(Keys.Alt)) mod |= MOD_ALT;
        if (keys.HasFlag(Keys.Control)) mod |= MOD_CONTROL;
        if (keys.HasFlag(Keys.Shift)) mod |= MOD_SHIFT;
        if (!RegisterHotKey(Handle, id, mod, (uint)(keys & Keys.KeyCode))) return false;
        registered.Add(id);
        return true;
    }

    public void Unregister(int id)
    {
        if (registered.Remove(id)) UnregisterHotKey(Handle, id);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY) Pressed?.Invoke((int)m.WParam);
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        foreach (var id in registered.ToList()) Unregister(id);
        DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
