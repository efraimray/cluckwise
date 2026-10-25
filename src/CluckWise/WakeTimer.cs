using Microsoft.Win32.SafeHandles;

namespace CluckWise;

/// <summary>
/// Asks Windows to wake the PC from sleep shortly before the next alarm that has
/// "Wake the PC" turned on. Windows must allow wake timers (Power Options), and
/// some laptops with Modern Standby may ignore them.
/// </summary>
public static class WakeTimer
{
    private static readonly TimeSpan Lead = TimeSpan.FromSeconds(60);
    private static SafeWaitHandle? _timer;
    private static DateTime? _armedFor;

    /// <summary>When the PC is set to wake next, or null if no wake is set.</summary>
    public static DateTime? ArmedFor => _armedFor;

    public static void Update(AppData data)
    {
        var now = DateTime.Now;
        DateTime? next = null;
        foreach (var a in data.Alarms)
        {
            if (a.IsNew || !a.WakeComputer) continue;
            var when = a.SnoozedUntil ?? (a.Enabled ? Scheduler.NextOccurrence(a, now) : null);
            if (when is { } w && (next is null || w < next)) next = w;
        }

        var wakeAt = next?.Add(-Lead);
        if (wakeAt == _armedFor) return;

        try
        {
            _timer ??= NativeMethods.CreateWaitableTimer(IntPtr.Zero, true, null);
            if (_timer.IsInvalid) return;
            if (wakeAt is { } at && at > now)
            {
                long due = at.ToUniversalTime().ToFileTimeUtc();
                NativeMethods.SetWaitableTimer(_timer, ref due, 0, IntPtr.Zero, IntPtr.Zero, resume: true);
                _armedFor = at;
            }
            else
            {
                NativeMethods.CancelWaitableTimer(_timer);
                _armedFor = null;
            }
        }
        catch
        {
            // Wake timers are a bonus; never let them stop alarms from working.
            _armedFor = null;
        }
    }
}
