using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CluckWise;

/// <summary>Small wrappers around the Windows functions CluckWise needs.</summary>
internal static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern SafeWaitHandle CreateWaitableTimer(IntPtr attributes, bool manualReset, string? name);

    // dueTime: absolute time as a FILETIME (UTC). resume = true wakes the PC from sleep.
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetWaitableTimer(SafeWaitHandle timer, ref long dueTime, int period,
        IntPtr completionRoutine, IntPtr argToCompletionRoutine, bool resume);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CancelWaitableTimer(SafeWaitHandle timer);

    [Flags]
    public enum ExecutionState : uint
    {
        SystemRequired = 0x00000001,
        DisplayRequired = 0x00000002,
        Continuous = 0x80000000,
    }

    [DllImport("kernel32.dll")]
    public static extern ExecutionState SetThreadExecutionState(ExecutionState flags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool LockWorkStation();

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public static readonly IntPtr HWND_BROADCAST = new(0xffff);
    public const int WM_SYSCOMMAND = 0x0112;
    public const int SC_MONITORPOWER = 0xF170;

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
    public const uint MOUSEEVENTF_MOVE = 0x0001;

    /// <summary>Turns the screen off (it comes back on with any mouse or key input).</summary>
    public static void MonitorOff() =>
        SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)2);

    /// <summary>Keeps the PC awake and the screen on while an alarm rings; call with false to stop.</summary>
    public static void KeepAwake(bool on)
    {
        SetThreadExecutionState(on
            ? ExecutionState.Continuous | ExecutionState.SystemRequired | ExecutionState.DisplayRequired
            : ExecutionState.Continuous);
        if (on)
        {
            // A tiny mouse nudge switches the screen back on after a wake from sleep.
            mouse_event(MOUSEEVENTF_MOVE, 1, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_MOVE, -1, 0, 0, UIntPtr.Zero);
        }
    }
}
