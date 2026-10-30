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

public enum RepeatMode { Once, Daily, Weekly, Monthly, Yearly, EveryNDays, MonthlyWeekday, Dates }

public enum ChallengeKind { None, Maths, TypePhrase, TapTiles }

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

    /// <summary>Extra times of day this alarm also rings at, in minutes after midnight (e.g. 1080 = 6:00 PM).</summary>
    public List<int> ExtraTimes { get; set; } = new();

    /// <summary>The main time plus any extra times, earliest first.</summary>
    public IEnumerable<TimeSpan> AllTimes() =>
        new[] { Hour * 60 + Minute }.Concat(ExtraTimes).Distinct().OrderBy(m => m).Select(m => TimeSpan.FromMinutes(m));

    [JsonIgnore]
    public string ExtraTimesText => ExtraTimes.Count == 0 ? "" :
        "Also at " + string.Join(", ", ExtraTimes.OrderBy(m => m).Select(m => TimeFormat.Time(DateTime.Today.AddMinutes(m))));

    // ---- Repeat details ----
    /// <summary>Weekly: ring every N weeks (1 = every week), counted from <see cref="OnceDate"/>.</summary>
    public int IntervalWeeks { get; set; } = 1;
    /// <summary>EveryNDays: ring every N days starting on <see cref="OnceDate"/>.</summary>
    public int IntervalDays { get; set; } = 2;
    /// <summary>MonthlyWeekday: 1–4 = first…fourth, 5 = last.</summary>
    public int NthWeek { get; set; } = 1;
    /// <summary>MonthlyWeekday: 0 = Sunday … 6 = Saturday.</summary>
    public int NthDay { get; set; } = 1;
    /// <summary>Dates: the specific days to ring on.</summary>
    public List<DateTime> SpecificDates { get; set; } = new();
    /// <summary>Repeating alarms stop after this day (inclusive). Null = no end.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>"Skip next time": this one ring time is skipped, then the alarm carries on as normal.</summary>
    public DateTime? SkipOccurrence { get; set; }

    // ---- Waking up ----
    public ChallengeKind Challenge { get; set; } = ChallengeKind.None;
    /// <summary>1 = easy, 2 = normal, 3 = hard.</summary>
    public int ChallengeLevel { get; set; } = 2;
    /// <summary>Say the time and label out loud when ringing.</summary>
    public bool SpeakOnRing { get; set; }
    /// <summary>Quick naps remove themselves once dismissed.</summary>
    public bool DeleteAfterRing { get; set; }

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
        RepeatMode.Once when DeleteAfterRing => "Quick nap · removes itself after",
        RepeatMode.Once => "Once · " + OnceDate.ToString("ddd d MMM"),
        RepeatMode.Daily => "Every day" + EndText,
        RepeatMode.Monthly => (OnceDate.Day >= 31 ? "Monthly on the last day" : $"Monthly on the {Ordinal(OnceDate.Day)}") + EndText,
        RepeatMode.Yearly => "Yearly on " + OnceDate.ToString("d MMMM") + EndText,
        RepeatMode.EveryNDays => (IntervalDays == 1 ? "Every day" : $"Every {IntervalDays} days") + EndText,
        RepeatMode.MonthlyWeekday => $"{NthName(NthWeek)} {((DayOfWeek)NthDay)} of the month" + EndText,
        RepeatMode.Dates => SpecificDates.Count switch
        {
            0 => "No dates chosen",
            1 => "On " + SpecificDates[0].ToString("ddd d MMM"),
            _ => $"On {SpecificDates.Count} dates · next " + (SpecificDates.Where(d => d.Date >= DateTime.Today).OrderBy(d => d).Select(d => d.ToString("d MMM")).FirstOrDefault() ?? "none"),
        },
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

    private string EndText => EndDate is { } e ? " · until " + e.ToString("d MMM") : "";

    public static string NthName(int n) => n switch { 1 => "First", 2 => "Second", 3 => "Third", 4 => "Fourth", _ => "Last" };

    private static string Ordinal(int n) => n + ((n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });

    private string WeeklyText()
    {
        string[] names = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        string prefix = IntervalWeeks > 1 ? $"Every {IntervalWeeks} weeks: " : "";
        if (Days.All(d => d)) return prefix + "Every day" + EndText;
        if (Days.SequenceEqual(new[] { false, true, true, true, true, true, false })) return prefix + "Weekdays" + EndText;
        if (Days.SequenceEqual(new[] { true, false, false, false, false, false, true })) return prefix + "Weekends" + EndText;
        var picked = names.Where((_, i) => Days[i]).ToArray();
        var days = picked.Length == 0 ? "No days chosen" : string.Join(", ", picked);
        return (IntervalWeeks > 1 ? $"Every {IntervalWeeks} weeks: " : "") + days + EndText;
    }

    /// <summary>Simple weekly alarms show S M T… chips on their card; everything else shows text.</summary>
    [JsonIgnore] public bool ShowDayChips => Repeat == RepeatMode.Weekly && IntervalWeeks <= 1 && EndDate == null;

    [JsonIgnore]
    public string SkipText => SkipOccurrence is { } s ? "Skipping " + s.ToString("ddd") + " " + TimeFormat.Time(s) : "";

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
        Raise(nameof(ExtraTimesText));
        Raise(nameof(ShowDayChips));
        Raise(nameof(SkipText));
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
        ExtraTimes = o.ExtraTimes.ToList();
        IntervalWeeks = o.IntervalWeeks;
        IntervalDays = o.IntervalDays;
        NthWeek = o.NthWeek;
        NthDay = o.NthDay;
        SpecificDates = o.SpecificDates.ToList();
        EndDate = o.EndDate;
        Challenge = o.Challenge;
        ChallengeLevel = o.ChallengeLevel;
        SpeakOnRing = o.SpeakOnRing;
        DeleteAfterRing = o.DeleteAfterRing;
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
    public ObservableCollection<TaskItem> TodoItems { get; set; } = new();
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
    /// <summary>"Soonest" = one list, next alarm first; "Groups" = grouped by Morning, Work….</summary>
    public string AlarmSort { get; set; } = "Soonest";
    public bool ShowWidget { get; set; }
    public bool HotkeysEnabled { get; set; } = true;

    // Vacation mode: no alarms (or none in VacationGroup) ring until VacationUntil.
    public DateTime? VacationUntil { get; set; }
    public string? VacationGroup { get; set; }
    public bool BriefingAloud { get; set; } = true;

    // Deep Work
    public int FocusMinutes { get; set; } = 25;
    public int BreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public bool FocusAutoRepeat { get; set; } = true;
    public bool QuietDuringFocus { get; set; } = true;
    public string FocusSound { get; set; } = "None";
    /// <summary>Focus minutes per day, keyed "yyyy-MM-dd".</summary>
    public Dictionary<string, int> FocusLog { get; set; } = new();
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
