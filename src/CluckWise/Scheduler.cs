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

            if (!alarm.Enabled) continue;
            var due = NextOccurrence(alarm, _lastCheck);
            if (due is { } d && d <= now && now - d <= CatchUpWindow && alarm.LastRung != d)
            {
                alarm.LastRung = d;
                if (alarm.Repeat == RepeatMode.Once) alarm.Enabled = false;
                AlarmDue?.Invoke(alarm);
            }
        }

        foreach (var timer in _data.Timers.ToList())
            if (timer.Tick(now)) TimerDone?.Invoke(timer);

        Morning.ResetIfNewDay(_data);
        _lastCheck = now;
        foreach (var alarm in _data.Alarms) alarm.Countdown = CountdownText(alarm, now);
        Ticked?.Invoke();
    }

    /// <summary>The first time strictly after <paramref name="after"/> that the alarm should ring.</summary>
    public static DateTime? NextOccurrence(Alarm alarm, DateTime after)
    {
        var time = new TimeSpan(alarm.Hour, alarm.Minute, 0);
        switch (alarm.Repeat)
        {
            case RepeatMode.Once:
                var once = alarm.OnceDate.Date + time;
                return once > after ? once : null;

            case RepeatMode.Daily:
                var today = after.Date + time;
                return today > after ? today : today.AddDays(1);

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
        if (alarm.SnoozedUntil is { } s) return "Snoozed until " + s.ToString("h:mm tt");
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
