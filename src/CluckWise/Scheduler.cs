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

    /// <summary>Raised when vacation mode ends by itself.</summary>
    public event Action? VacationEnded;

    public static void ApplyVacation(AppData data)
    {
        VacationUntil = data.VacationUntil;
        VacationGroup = data.VacationGroup;
    }

    public Scheduler(AppData data)
    {
        _data = data;
        ApplyVacation(data);
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

        foreach (var alarm in _data.Alarms)
            if (alarm.SkipOccurrence is { } skip && skip < now) { alarm.SkipOccurrence = null; alarm.Refresh(); }
        if (_data.VacationUntil is { } back && back <= now)
        {
            _data.VacationUntil = null;
            _data.VacationGroup = null;
            ApplyVacation(_data);
            VacationEnded?.Invoke();
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

    /// <summary>
    /// Vacation mode: alarms (all, or one group) don't ring until this moment. Set from AppData by the app.
    /// </summary>
    public static DateTime? VacationUntil { get; set; }
    public static string? VacationGroup { get; set; }

    public static bool IsOnVacation(Alarm alarm, DateTime at) =>
        VacationUntil is { } until && at < until
        && (VacationGroup == null || string.Equals(VacationGroup, alarm.Group, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The first time strictly after <paramref name="after"/> that the alarm should ring,
    /// honouring vacation mode, "skip next time" and the alarm's end date.
    /// </summary>
    public static DateTime? NextOccurrence(Alarm alarm, DateTime after)
    {
        // On vacation: look for the first ring from the day you're back.
        if (IsOnVacation(alarm, after) && VacationUntil is { } back) after = back.AddTicks(-1);

        var next = NextRaw(alarm, after);
        if (next is { } n && alarm.SkipOccurrence == n) next = NextRaw(alarm, n);
        if (next is { } m && alarm.Repeat != RepeatMode.Once && alarm.EndDate is { } end && m.Date > end.Date) return null;
        return next;
    }

    private static DateTime? NextRaw(Alarm alarm, DateTime after)
    {
        // An alarm can ring at several times a day (e.g. a class at 9:00 and 18:00): take the soonest.
        DateTime? best = null;
        foreach (var time in alarm.AllTimes())
        {
            var next = NextFor(alarm, after, time);
            if (next is { } n && (best is null || n < best)) best = n;
        }
        return best;
    }

    // Weeks are counted from a fixed Sunday so "every 2 weeks" lines up with the start date's week.
    private static int WeekIndex(DateTime d) => (int)Math.Floor((d.Date - new DateTime(2000, 1, 2)).TotalDays / 7);

    /// <summary>The Nth (1–4) or last (5) given weekday of a month, e.g. the first Monday.</summary>
    public static DateTime NthWeekday(int year, int month, int nth, DayOfWeek day)
    {
        if (nth >= 5)
        {
            var last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            return last.AddDays(-(((int)last.DayOfWeek - (int)day + 7) % 7));
        }
        var first = new DateTime(year, month, 1);
        return first.AddDays(((int)day - (int)first.DayOfWeek + 7) % 7 + 7 * (nth - 1));
    }

    private static DateTime? NextFor(Alarm alarm, DateTime after, TimeSpan time)
    {
        switch (alarm.Repeat)
        {
            case RepeatMode.EveryNDays:
            {
                int n = Math.Max(1, alarm.IntervalDays);
                var anchor = alarm.OnceDate.Date;
                var start = after.Date < anchor ? anchor : after.Date;
                for (int i = 0; i <= n + 1; i++)
                {
                    var day = start.AddDays(i);
                    if ((day - anchor).Days % n != 0) continue;
                    var candidate = day + time;
                    if (candidate > after) return candidate;
                }
                return null;
            }

            case RepeatMode.MonthlyWeekday:
                for (int i = 0; i <= 2; i++)
                {
                    var month = new DateTime(after.Year, after.Month, 1).AddMonths(i);
                    var candidate = NthWeekday(month.Year, month.Month, alarm.NthWeek, (DayOfWeek)alarm.NthDay) + time;
                    if (candidate > after) return candidate;
                }
                return null;

            case RepeatMode.Dates:
                return alarm.SpecificDates.Select(d => d.Date + time).Where(c => c > after).OrderBy(c => c)
                    .Select(c => (DateTime?)c).FirstOrDefault();

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
            {
                // Weekly on chosen days; with IntervalWeeks > 1, only in weeks that line up with the start date.
                int every = Math.Max(1, alarm.IntervalWeeks);
                int anchorWeek = WeekIndex(alarm.OnceDate);
                var from = every > 1 && after.Date < alarm.OnceDate.Date ? alarm.OnceDate.Date.AddTicks(-1) : after;
                for (int i = 0; i <= 7 * every + 7; i++)
                {
                    var candidate = from.Date.AddDays(i) + time;
                    if (candidate <= after || candidate <= from || !alarm.Days[(int)candidate.DayOfWeek]) continue;
                    if (every > 1 && (WeekIndex(candidate) - anchorWeek) % every != 0) continue;
                    return candidate;
                }
                return null;
            }
        }
    }

    public static string CountdownText(Alarm alarm, DateTime now)
    {
        if (alarm.SnoozedUntil is { } s) return (alarm.DeleteAfterRing ? "Rings at " : "Snoozed until ") + TimeFormat.Time(s);
        if (!alarm.Enabled) return "Off";
        if (NextOccurrence(alarm, now) is not { } next) return "Not scheduled";
        var text = "in " + Humanize(next - now);
        if (IsOnVacation(alarm, now)) text = "Paused · " + text;
        else if (alarm.SkipOccurrence is { } skip && skip > now) text += " (skipping " + skip.ToString("ddd") + ")";
        return text;
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
