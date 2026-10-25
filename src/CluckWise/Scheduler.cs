using System.Windows.Threading;

namespace CluckWise;

/// <summary>Checks the alarms every second and raises <see cref="AlarmDue"/> when one should ring.</summary>
public class Scheduler
{
    /// <summary>Alarms missed while the PC was asleep still ring if they are at most this late.</summary>
    private static readonly TimeSpan CatchUpWindow = TimeSpan.FromMinutes(30);

    private readonly AppData _data;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime _lastCheck = DateTime.Now;

    public event Action<Alarm>? AlarmDue;
    public event Action? Ticked;
    public event Action<CountdownTimer>? TimerDone;
    /// <summary>Raised once, N minutes before an alarm that has a reminder set.</summary>
    public event Action<Alarm, DateTime>? ReminderDue;
    /// <summary>Raised once a year at 9 AM (or when CluckWise starts later that day) on someone's birthday.</summary>
    public event Action<Birthday>? BirthdayDue;
    /// <summary>Raised once a year at 9 AM the day before a birthday.</summary>
    public event Action<Birthday>? BirthdayTomorrow;
    /// <summary>Raised when a task with a due time (or its snooze) comes due.</summary>
    public event Action<TaskItem>? TaskDue;
    private static readonly TimeSpan BirthdayTime = new(9, 0, 0);
    private DateTime _lastWakeUpdate = DateTime.MinValue;

    public Scheduler(AppData data)
    {
        _data = data;
        _timer.Tick += (_, _) => Check();
    }

    public void Start() => _timer.Start();

    private void Check()
    {
        var now = DateTime.Now;
        foreach (var alarm in _data.Alarms.ToList())
        {
            if (alarm.SnoozedUntil is { } until && until <= now)
            {
                alarm.SnoozedUntil = null;
                AlarmDue?.Invoke(alarm);
                continue;
            }

            if (DueNow(alarm, _lastCheck, now) is { } d)
            {
                alarm.LastRung = d;
                // A one-time alarm turns off after its last time has rung.
                if (alarm.Repeat == RepeatMode.Once && NextOccurrence(alarm, d) == null) alarm.Enabled = false;
                AlarmDue?.Invoke(alarm);
            }
        }

        foreach (var timer in _data.Timers.ToList())
            if (timer.Tick(now)) TimerDone?.Invoke(timer);

        foreach (var alarm in _data.Alarms.ToList())
        {
            if (!alarm.Enabled || alarm.IsNew || alarm.ReminderMinutes <= 0) continue;
            if (NextOccurrence(alarm, now) is not { } next) continue;
            var remindAt = next.AddMinutes(-alarm.ReminderMinutes);
            if (now >= remindAt && now < next && alarm.LastReminderFor != next)
            {
                alarm.LastReminderFor = next;
                ReminderDue?.Invoke(alarm, next);
            }
        }

        if (now - _lastWakeUpdate > TimeSpan.FromSeconds(30))
        {
            _lastWakeUpdate = now;
            WakeTimer.Update(_data);
        }

        if (now.TimeOfDay >= BirthdayTime)
        {
            foreach (var b in _data.Birthdays.ToList())
            {
                int days = (b.Next(now.Date) - now.Date).Days;
                if (days == 0 && b.LastCelebratedYear != now.Year)
                {
                    b.LastCelebratedYear = now.Year;
                    BirthdayDue?.Invoke(b);
                }
                else if (days == 1 && b.LastHeadsUpYear != now.Year)
                {
                    b.LastHeadsUpYear = now.Year;
                    BirthdayTomorrow?.Invoke(b);
                }
            }
        }

        foreach (var task in _data.TodoItems.ToList())
        {
            if (task.Done || task.Reminded || !task.HasTime || task.Due is not { } due) continue;
            var at = task.SnoozedUntil ?? due;
            // Don't pop up for tasks that went overdue long ago (e.g. while the PC was off for days).
            if (now >= at && now - at < TimeSpan.FromHours(12))
            {
                task.Reminded = true;
                TaskDue?.Invoke(task);
            }
        }

        Morning.ResetIfNewDay(_data);
        _lastCheck = now;
        foreach (var alarm in _data.Alarms) alarm.Countdown = CountdownText(alarm, now);
        Ticked?.Invoke();
    }

    /// <summary>
    /// If the alarm should ring now (it came due since the last check, isn't more than
    /// 30 minutes late, and hasn't rung for that time yet), returns the time it was due.
    /// </summary>
    public static DateTime? DueNow(Alarm alarm, DateTime lastCheck, DateTime now)
    {
        if (!alarm.Enabled || alarm.IsNew) return null;
        var due = NextOccurrence(alarm, lastCheck);
        return due is { } d && d <= now && now - d <= CatchUpWindow && alarm.LastRung != d ? d : null;
    }

    /// <summary>The first time strictly after <paramref name="after"/> that the alarm should ring.</summary>
    public static DateTime? NextOccurrence(Alarm alarm, DateTime after)
    {
        // An alarm can ring at several times a day (e.g. a class at 9:00 and 18:00): take the soonest.
        DateTime? best = null;
        foreach (var time in alarm.AllTimes())
        {
            var next = NextOccurrence(alarm, after, time);
            if (next is { } n && (best is null || n < best)) best = n;
        }
        return best;
    }

    private static DateTime? NextOccurrence(Alarm alarm, DateTime after, TimeSpan time)
    {
        switch (alarm.Repeat)
        {
            case RepeatMode.Once:
                var once = alarm.OnceDate.Date + time;
                return once > after ? once : null;

            case RepeatMode.Daily:
                var today = after.Date + time;
                return today > after ? today : today.AddDays(1);

            case RepeatMode.Monthly:
                // e.g. "the 31st" falls on the last day in shorter months
                for (int i = 0; i <= 2; i++)
                {
                    var month = new DateTime(after.Year, after.Month, 1).AddMonths(i);
                    int day = Math.Min(alarm.OnceDate.Day, DateTime.DaysInMonth(month.Year, month.Month));
                    var candidate = new DateTime(month.Year, month.Month, day) + time;
                    if (candidate > after) return candidate;
                }
                return null;

            case RepeatMode.Yearly:
                for (int i = 0; i <= 1; i++)
                {
                    int year = after.Year + i;
                    int day = Math.Min(alarm.OnceDate.Day, DateTime.DaysInMonth(year, alarm.OnceDate.Month)); // 29 Feb -> 28 Feb
                    var candidate = new DateTime(year, alarm.OnceDate.Month, day) + time;
                    if (candidate > after) return candidate;
                }
                return null;

            default:
                for (int i = 0; i <= 7; i++)
                {
                    var candidate = after.Date.AddDays(i) + time;
                    if (candidate > after && alarm.Days[(int)candidate.DayOfWeek]) return candidate;
                }
                return null;
        }
    }

    public static string CountdownText(Alarm alarm, DateTime now)
    {
        if (alarm.SnoozedUntil is { } s) return "Snoozed until " + TimeFormat.Time(s);
        if (!alarm.Enabled) return "Off";
        if (NextOccurrence(alarm, now) is not { } next) return "Not scheduled";
        return "in " + Humanize(next - now);
    }

    public static string Humanize(TimeSpan span)
    {
        if (span.TotalMinutes < 1) return "less than a minute";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} min";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours} h {span.Minutes} min";
        int days = (int)span.TotalDays;
        return $"{days} day{(days == 1 ? "" : "s")} {span.Hours} h";
    }

    /// <summary>The enabled alarm that rings soonest, for the "Next alarm" card.</summary>
    public static (Alarm alarm, DateTime when)? NextAlarm(IEnumerable<Alarm> alarms, DateTime now)
    {
        (Alarm, DateTime)? best = null;
        foreach (var a in alarms)
        {
            var when = a.SnoozedUntil ?? (a.Enabled ? NextOccurrence(a, now) : null);
            if (when is { } w && (best is null || w < best.Value.Item2)) best = (a, w);
        }
        return best;
    }
}
