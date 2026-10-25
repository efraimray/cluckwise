namespace CluckWise;

/// <summary>One item on the morning checklist, e.g. "Drink a glass of water".</summary>
public class MorningTask : Observable
{
    private string _text = "";
    private bool _done;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get => _text; set => Set(ref _text, value); }
    public bool Done { get => _done; set => Set(ref _done, value); }
}

/// <summary>
/// The morning routine: the checklist resets every day, and the streak counts
/// morning alarms in a row that were dismissed without snoozing.
/// </summary>
public static class Morning
{
    public static event Action? Changed;

    /// <summary>Alarms set before noon count as morning alarms.</summary>
    public static bool IsMorningAlarm(Alarm alarm) => alarm.Hour < 12;

    /// <summary>Unticks everything once a new day starts.</summary>
    public static void ResetIfNewDay(AppData data)
    {
        if (data.TasksDate.Date == DateTime.Today) return;
        foreach (var t in data.Tasks) t.Done = false;
        data.TasksDate = DateTime.Today;
        Changed?.Invoke();
    }

    /// <summary>Called when a morning alarm is dismissed.</summary>
    public static void RecordDismiss(AppData data, Alarm alarm, DateTime? onDay = null)
    {
        if (!IsMorningAlarm(alarm)) return;
        var today = (onDay ?? DateTime.Today).Date;
        if (alarm.SnoozedThisRing)
        {
            // Snoozing breaks the streak, but only once per morning.
            if (data.LastOnTimeDate != today) data.Streak = 0;
        }
        else if (data.LastOnTimeDate != today)
        {
            data.Streak++;
            data.LastOnTimeDate = today;
            data.BestStreak = Math.Max(data.BestStreak, data.Streak);
        }
        alarm.SnoozedThisRing = false;
        Changed?.Invoke();
    }

    public static string StreakText(AppData data) => data.Streak switch
    {
        0 => "Start your streak tomorrow",
        1 => "1 morning on time",
        _ => $"{data.Streak} mornings on time",
    };

    public static void NotifyChanged() => Changed?.Invoke();
}
