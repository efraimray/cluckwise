using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace CluckWise;

/// <summary>System-wide keyboard shortcuts (Ctrl+Alt+…) that work even when CluckWise is in the tray.</summary>
public static class GlobalHotkeys
{
    public record Hotkey(int Id, Key Key, string Keys, string Does, Action Run);

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private static HwndSource? _source;
    private static readonly List<Hotkey> _registered = new();

    public static List<Hotkey> All { get; } = new();

    /// <summary>Shortcuts another app already uses, so they couldn't be registered.</summary>
    public static List<Hotkey> Failed { get; } = new();

    public static void Define(List<Hotkey> keys)
    {
        All.Clear();
        All.AddRange(keys);
    }

    public static void Enable()
    {
        Disable();
        if (_source == null)
        {
            // A hidden message-only window receives the WM_HOTKEY messages.
            var p = new HwndSourceParameters("CluckWiseHotkeys") { ParentWindow = new IntPtr(-3), WindowStyle = 0 };
            _source = new HwndSource(p);
            _source.AddHook(WndProc);
        }
        Failed.Clear();
        foreach (var k in All)
        {
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(k.Key);
            if (RegisterHotKey(_source.Handle, k.Id, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, vk)) _registered.Add(k);
            else Failed.Add(k);
        }
    }

    public static void Disable()
    {
        if (_source != null)
            foreach (var k in _registered) UnregisterHotKey(_source.Handle, k.Id);
        _registered.Clear();
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _registered.FirstOrDefault(k => k.Id == wParam.ToInt32()) is { } hk)
        {
            hk.Run();
            handled = true;
        }
        return IntPtr.Zero;
    }
}
