namespace CluckWise;

/// <summary>The morning briefing: today's tasks, birthdays and next alarm, as short lines and as one spoken sentence.</summary>
public static class Briefing
{
    public static List<string> Lines(AppData data, DateTime now)
    {
        var lines = new List<string>();
        var today = now.Date;

        var open = data.TodoItems.Where(t => !t.Done).ToList();
        var dueToday = open.Where(t => t.Due is { } d && d.Date == today).OrderBy(t => t.Due).ToList();
        var overdue = open.Where(t => t.SectionAt(now) == "Overdue" && !(t.Due is { } d && d.Date == today)).ToList();
        if (dueToday.Count > 0)
        {
            var first = dueToday.FirstOrDefault(t => t.HasTime && t.Due >= now) ?? dueToday[0];
            var when = first.HasTime && first.Due is { } fd ? " at " + TimeFormat.Time(fd) : "";
            lines.Add(dueToday.Count == 1
                ? $"1 task today: {first.Title}{when}"
                : $"{dueToday.Count} tasks today · first: {first.Title}{when}");
        }
        if (overdue.Count > 0) lines.Add(overdue.Count == 1 ? $"Overdue: {overdue[0].Title}" : $"{overdue.Count} tasks are overdue");

        foreach (var b in data.Birthdays.Where(b => b.Next(today) == today))
            lines.Add(b.TurningAge(today) is { } age ? $"Birthday: {b.Name} turns {age} today" : $"Birthday: it's {b.Name}'s birthday");

        if (Scheduler.NextAlarm(data.Alarms.Where(a => !a.IsNew), now) is { } n)
            lines.Add($"Next alarm: {TimeFormat.Time(n.when)} · {n.alarm.Label}" + (n.when.Date == today ? "" : $" ({n.when:dddd})"));

        return lines;
    }

    /// <summary>"Good morning! It's 6:30 AM. Rise and shine." — said when an alarm rings.</summary>
    public static string Greeting(DateTime now, string label)
    {
        string hello = now.Hour < 12 ? "Good morning" : now.Hour < 18 ? "Good afternoon" : "Good evening";
        var said = $"{hello}! It's {TimeFormat.Time(now)}.";
        return string.IsNullOrWhiteSpace(label) ? said : $"{said} {label.Trim().TrimEnd('.')}.";
    }

    /// <summary>The briefing as one short paragraph to read aloud.</summary>
    public static string Spoken(List<string> lines) =>
        lines.Count == 0 ? "" : "Here's your day. " + string.Join(". ", lines.Select(l => l.Replace(" · ", ", "))) + ".";
}
