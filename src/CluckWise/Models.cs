using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace CluckWise;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }
}

public enum RepeatMode { Once, Daily, Weekly, Monthly, Yearly }

public enum ActionKind { OpenWebsite, OpenProgram, PlayMusic, LockPC, ScreenOff, Sleep }

public class AlarmAction
{
    public ActionKind Kind { get; set; }
    public string Target { get; set; } = "";

    [JsonIgnore]
    public string Description => Kind switch
    {
        ActionKind.OpenWebsite => $"Open website: {Target}",
        ActionKind.PlayMusic => $"Play: {System.IO.Path.GetFileName(Target)}",
        ActionKind.LockPC => "Lock the PC",
        ActionKind.ScreenOff => "Turn off the screen",
        ActionKind.Sleep => "Put the PC to sleep (after a 30-second countdown)",
        _ => $"Open: {Target}",
    };
}

public class Alarm : Observable
{
    private bool _enabled = true;
    private string _countdown = "";

    public Guid Id { get; set; } = Guid.NewGuid();
    public int Hour { get; set; } = 7;
    public int Minute { get; set; }
    public string Label { get; set; } = "Alarm";
    public string Group { get; set; } = "Morning";
    public RepeatMode Repeat { get; set; } = RepeatMode.Weekly;

    /// <summary>Index 0 = Sunday … 6 = Saturday (matches DayOfWeek).</summary>
    public bool[] Days { get; set; } = { false, true, true, true, true, true, false };

    /// <summary>Date for one-time alarms; for monthly/yearly alarms, the day (and month) they repeat on.</summary>
    public DateTime OnceDate { get; set; } = DateTime.Today;

    public string Sound { get; set; } = Sounds.RoosterCrow;
    public int Volume { get; set; } = 80;
    public bool GentleWake { get; set; } = true;
    public int SnoozeMinutes { get; set; } = 9;
    public bool RunSilently { get; set; }
    public bool WakeComputer { get; set; } = true;

    /// <summary>Minutes before the alarm to show a quiet heads-up (0 = off).</summary>
    public int ReminderMinutes { get; set; }

    /// <summary>The alarm time a reminder was last shown for, so it only shows once.</summary>
    public DateTime? LastReminderFor { get; set; }
    public List<AlarmAction> Actions { get; set; } = new();

    public DateTime? LastRung { get; set; }
    public DateTime? SnoozedUntil { get; set; }

    public bool Enabled
    {
        get => _enabled;
        set { if (Set(ref _enabled, value)) Raise(nameof(SummaryText)); }
    }

    [JsonIgnore] public string TimeText => TimeFormat.Digits(Hour, Minute);
    [JsonIgnore] public string AmPm => TimeFormat.Suffix(Hour);

    [JsonIgnore]
    public string RepeatText => Repeat switch
    {
        RepeatMode.Once => "Once · " + OnceDate.ToString("ddd d MMM"),
        RepeatMode.Daily => "Every day",
        RepeatMode.Monthly => $"Monthly on the {Ordinal(OnceDate.Day)}",
        RepeatMode.Yearly => "Yearly on " + OnceDate.ToString("d MMMM"),
        _ => WeeklyText(),
    };

    [JsonIgnore]
    public string SummaryText
    {
        get
        {
            var parts = new List<string> { RepeatText };
            if (Actions.Count > 0) parts.Add(Actions.Count == 1 ? "1 action" : $"{Actions.Count} actions");
            return string.Join(" · ", parts);
        }
    }

    private bool _isExpanded;

    /// <summary>True while this alarm's card is open for editing in the main window.</summary>
    [JsonIgnore]
    public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }

    /// <summary>True for a just-added alarm that hasn't been saved yet (Cancel removes it).</summary>
    [JsonIgnore]
    public bool IsNew { get; set; }

    /// <summary>True once this alarm has been snoozed during the current ring (used for the streak).</summary>
    [JsonIgnore]
    public bool SnoozedThisRing { get; set; }

    [JsonIgnore]
    public string Countdown
    {
        get => _countdown;
        set => Set(ref _countdown, value);
    }

    private static string Ordinal(int n) => n + ((n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });

    private string WeeklyText()
    {
        string[] names = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        if (Days.All(d => d)) return "Every day";
        if (Days.SequenceEqual(new[] { false, true, true, true, true, true, false })) return "Weekdays";
        if (Days.SequenceEqual(new[] { true, false, false, false, false, false, true })) return "Weekends";
        var picked = names.Where((_, i) => Days[i]).ToArray();
        return picked.Length == 0 ? "No days chosen" : string.Join(", ", picked);
    }

    /// <summary>Call after editing so the list refreshes its text.</summary>
    public void Refresh()
    {
        Raise(nameof(TimeText));
        Raise(nameof(Days));
        Raise(nameof(Repeat));
        Raise(nameof(AmPm));
        Raise(nameof(RepeatText));
        Raise(nameof(SummaryText));
        Raise(nameof(Label));
        Raise(nameof(Group));
        Raise(nameof(Sound));
    }

    public Alarm Clone()
    {
        var copy = new Alarm { Id = Id };
        copy.CopyFrom(this);
        return copy;
    }

    /// <summary>Copies the editable settings (not the Id or ring history).</summary>
    public void CopyFrom(Alarm o)
    {
        Hour = o.Hour;
        Minute = o.Minute;
        Label = o.Label;
        Group = o.Group;
        Repeat = o.Repeat;
        Days = (bool[])o.Days.Clone();
        OnceDate = o.OnceDate;
        Sound = o.Sound;
        Volume = o.Volume;
        GentleWake = o.GentleWake;
        SnoozeMinutes = o.SnoozeMinutes;
        RunSilently = o.RunSilently;
        WakeComputer = o.WakeComputer;
        ReminderMinutes = o.ReminderMinutes;
        Actions = o.Actions.Select(a => new AlarmAction { Kind = a.Kind, Target = a.Target }).ToList();
        Enabled = o.Enabled;
        Refresh();
    }
}

public class AppData
{
    public int Version { get; set; } = 1;
    public ObservableCollection<Alarm> Alarms { get; set; } = new();
    public ObservableCollection<CountdownTimer> Timers { get; set; } = new();
    public ObservableCollection<WorldClock> WorldClocks { get; set; } = new();
    public ObservableCollection<Birthday> Birthdays { get; set; } = new();
    public bool StartWithWindows { get; set; }
    public bool ShowedTrayTip { get; set; }
    public bool AskedAboutStartup { get; set; }
    public string DefaultSound { get; set; } = Sounds.RoosterCrow;
    public int DefaultSnoozeMinutes { get; set; } = 9;

    // Morning routine
    public ObservableCollection<MorningTask> Tasks { get; set; } = new();
    public DateTime TasksDate { get; set; } = DateTime.Today;
    public bool ShowTodoAfterMorningAlarm { get; set; } = true;
    public bool SeededTasks { get; set; }
    /// <summary>The order of alarm groups on the Alarms page (set by dragging group headings).</summary>
    public List<string> GroupOrder { get; set; } = new();
    public bool Use24Hour { get; set; }
    public bool DarkMode { get; set; }
    public bool ShowWidget { get; set; }
    public double? WidgetLeft { get; set; }
    public double? WidgetTop { get; set; }
    public int Streak { get; set; }
    public int BestStreak { get; set; }
    public DateTime? LastOnTimeDate { get; set; }

    /// <summary>A copy for saving that leaves out alarms still being created.</summary>
    public AppData ForSaving()
    {
        var copy = (AppData)MemberwiseClone();
        copy.Alarms = new(Alarms.Where(a => !a.IsNew));
        return copy;
    }
}
