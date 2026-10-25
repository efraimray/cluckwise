using System.Windows.Threading;

namespace CluckWise;

public enum FocusPhase { Idle, Focus, Break }

/// <summary>
/// Deep Work sessions: focus for N minutes, then a short break (a longer one every 4th),
/// optionally repeating. While focusing, reminders wait until the break (alarms still ring).
/// </summary>
public static class DeepWork
{
    private static readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private static DateTime _phaseEnds;
    private static TimeSpan _pausedLeft;
    private static DateTime _focusStarted;

    public static FocusPhase Phase { get; private set; } = FocusPhase.Idle;
    public static bool Paused { get; private set; }
    public static string Task { get; private set; } = "";
    public static TimeSpan PhaseLength { get; private set; }
    public static int SessionsDone { get; private set; }

    public static TimeSpan Remaining => Paused ? _pausedLeft : (Phase == FocusPhase.Idle ? TimeSpan.Zero : Max(_phaseEnds - DateTime.Now));
    public static double Progress => PhaseLength.TotalSeconds <= 0 ? 0 : 1 - Remaining.TotalSeconds / PhaseLength.TotalSeconds;

    /// <summary>Raised every second while a session runs, and whenever the phase changes.</summary>
    public static event Action? Changed;

    static DeepWork() => _timer.Tick += (_, _) => Tick();

    public static void Start(string task)
    {
        var d = App.Current.Data;
        Task = string.IsNullOrWhiteSpace(task) ? "Deep work" : task.Trim();
        SessionsDone = 0;
        BeginPhase(FocusPhase.Focus, TimeSpan.FromMinutes(Math.Clamp(d.FocusMinutes, 1, 240)));
        _timer.Start();
    }

    public static void TogglePause()
    {
        if (Phase == FocusPhase.Idle) return;
        if (Paused) { _phaseEnds = DateTime.Now + _pausedLeft; Paused = false; }
        else { _pausedLeft = Max(_phaseEnds - DateTime.Now); Paused = true; }
        Changed?.Invoke();
    }

    /// <summary>Ends the break early and starts the next focus session.</summary>
    public static void SkipBreak()
    {
        if (Phase == FocusPhase.Break) BeginPhase(FocusPhase.Focus, TimeSpan.FromMinutes(App.Current.Data.FocusMinutes));
    }

    public static void Stop()
    {
        if (Phase == FocusPhase.Focus) RecordFocus(PhaseLength - Remaining); // count the part actually focused
        Phase = FocusPhase.Idle;
        Paused = false;
        _timer.Stop();
        SetQuiet(false);
        Ambient.Stop();
        App.Current.SaveData();
        Changed?.Invoke();
    }

    private static void BeginPhase(FocusPhase phase, TimeSpan length)
    {
        Phase = phase;
        Paused = false;
        PhaseLength = length;
        _phaseEnds = DateTime.Now + length;
        var d = App.Current.Data;
        if (phase == FocusPhase.Focus)
        {
            _focusStarted = DateTime.Now;
            SetQuiet(d.QuietDuringFocus);
            Ambient.Play(d.FocusSound);
        }
        else
        {
            SetQuiet(false);
            Ambient.Stop();
        }
        Changed?.Invoke();
    }

    private static void Tick()
    {
        if (Phase == FocusPhase.Idle || Paused) { Changed?.Invoke(); return; }
        if (DateTime.Now < _phaseEnds) { Changed?.Invoke(); return; }

        var d = App.Current.Data;
        if (Phase == FocusPhase.Focus)
        {
            RecordFocus(PhaseLength);
            SessionsDone++;
            Sounds.Play(Sounds.SunriseChime, 70, gentle: false, loop: false);
            bool longBreak = SessionsDone % 4 == 0;
            BeginPhase(FocusPhase.Break, TimeSpan.FromMinutes(longBreak ? d.LongBreakMinutes : d.BreakMinutes));
            App.Current.ShowTrayMessage("Focus session done", longBreak ? "Great work. Take a longer break." : "Nice! Take a short break.");
        }
        else
        {
            Sounds.Play(Sounds.GentleBeep, 60, gentle: false, loop: false);
            if (d.FocusAutoRepeat) BeginPhase(FocusPhase.Focus, TimeSpan.FromMinutes(d.FocusMinutes));
            else
            {
                App.Current.ShowTrayMessage("Break's over", "Start another focus session when you're ready.");
                Stop();
            }
        }
    }

    private static void SetQuiet(bool on)
    {
        App.Current.InFocus = on;
        if (!on) App.Current.ReleaseHeld();
    }

    private static void RecordFocus(TimeSpan span)
    {
        int minutes = (int)Math.Round(span.TotalMinutes);
        if (minutes <= 0) return;
        var key = DateTime.Today.ToString("yyyy-MM-dd");
        var log = App.Current.Data.FocusLog;
        log[key] = (log.TryGetValue(key, out var m) ? m : 0) + minutes;
        App.Current.SaveData();
    }

    // ---- stats ---------------------------------------------------------------------

    public static int MinutesOn(Dictionary<string, int> log, DateTime day) =>
        log.TryGetValue(day.ToString("yyyy-MM-dd"), out var m) ? m : 0;

    /// <summary>Monday-to-today total for this week.</summary>
    public static int MinutesThisWeek(Dictionary<string, int> log, DateTime today)
    {
        int back = ((int)today.DayOfWeek + 6) % 7; // days since Monday
        return Enumerable.Range(0, back + 1).Sum(i => MinutesOn(log, today.AddDays(-i)));
    }

    /// <summary>Days in a row with some focus time, counting today if you've focused already, else up to yesterday.</summary>
    public static int Streak(Dictionary<string, int> log, DateTime today)
    {
        var day = MinutesOn(log, today) > 0 ? today : today.AddDays(-1);
        int streak = 0;
        while (MinutesOn(log, day) > 0) { streak++; day = day.AddDays(-1); }
        return streak;
    }

    private static TimeSpan Max(TimeSpan t) => t < TimeSpan.Zero ? TimeSpan.Zero : t;
}
