using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ScreenChanger.Services;

public class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_NOREPEAT = 0x4000;

    private readonly Dictionary<int, Action> _hotkeys = new();
    private HwndSource? _source;
    private IntPtr _handle;

    public void Initialize(IntPtr handle)
    {
        _handle = handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WndProc);
    }

    public bool Register(int id, uint modifiers, uint key, Action callback)
    {
        bool ok = RegisterHotKey(_handle, id, modifiers | MOD_NOREPEAT, key);
        if (ok) _hotkeys[id] = callback;
        return ok;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _hotkeys.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _hotkeys.Keys)
            UnregisterHotKey(_handle, id);
        _source?.RemoveHook(WndProc);
    }
}
