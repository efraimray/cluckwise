using System.Text.Json.Serialization;

namespace CluckWise;

/// <summary>A countdown timer (e.g. "Tea, 5 min"). Several can run at once.</summary>
public class CountdownTimer : Observable
{
    private string _label = "Timer";
    private double _remaining;
    private bool _running;

    public Guid Id { get; set; } = Guid.NewGuid();
    public int DurationSeconds { get; set; } = 300;
    public string Sound { get; set; } = Sounds.SunriseChime;

    /// <summary>When a running timer will finish; saved so timers survive closing the app.</summary>
    public DateTime? EndsAt { get; set; }

    public string Label { get => _label; set => Set(ref _label, value); }

    public double RemainingSeconds
    {
        get => _remaining;
        set { if (Set(ref _remaining, value)) { Raise(nameof(RemainingText)); Raise(nameof(Progress)); } }
    }

    public bool Running
    {
        get => _running;
        set { if (Set(ref _running, value)) Raise(nameof(StartPauseText)); }
    }

    [JsonIgnore] public string RemainingText => Format(Math.Ceiling(RemainingSeconds));
    [JsonIgnore] public string DurationText => Format(DurationSeconds);
    [JsonIgnore] public double Progress => DurationSeconds <= 0 ? 0 : 1 - RemainingSeconds / DurationSeconds;
    [JsonIgnore] public string StartPauseText => Running ? "Pause" : RemainingSeconds < DurationSeconds ? "Resume" : "Start";

    public void Start()
    {
        if (RemainingSeconds <= 0) RemainingSeconds = DurationSeconds;
        EndsAt = DateTime.Now.AddSeconds(RemainingSeconds);
        Running = true;
    }

    public void Pause()
    {
        Tick(DateTime.Now);
        EndsAt = null;
        Running = false;
    }

    public void Reset()
    {
        EndsAt = null;
        Running = false;
        RemainingSeconds = DurationSeconds;
        Raise(nameof(StartPauseText));
    }

    /// <summary>Updates the remaining time. Returns true at the moment the timer finishes.</summary>
    public bool Tick(DateTime now)
    {
        if (!Running || EndsAt is not { } end) return false;
        RemainingSeconds = Math.Max(0, (end - now).TotalSeconds);
        if (RemainingSeconds > 0) return false;
        Running = false;
        EndsAt = null;
        Raise(nameof(StartPauseText));
        return true;
    }

    private static string Format(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
    }
}
