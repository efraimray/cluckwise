using System.Globalization;
using System.Text.RegularExpressions;

namespace CluckWise;

/// <summary>
/// Understands quick-add text such as "Call Mum at 3pm", "Pay rent tomorrow 9am !high",
/// "Stretch in 20 min" or "Gym on friday 6:30 pm". Whatever isn't a date, time or priority is the title.
/// </summary>
public static class TaskParser
{
    public record Result(string Title, DateTime? Due, bool HasTime, TaskPriority Priority);

    private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly string[] DayNames = { "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" };

    public static Result Parse(string input, DateTime now)
    {
        var text = " " + input.Trim() + " ";
        var priority = TaskPriority.Normal;
        DateTime? date = null;
        TimeSpan? time = null;

        // Priority: "!high", "!low", or a lone "!" / "!!"
        if (Take(ref text, @"\s!(high|urgent|important)\b", out _)) priority = TaskPriority.High;
        else if (Take(ref text, @"\s!low\b", out _)) priority = TaskPriority.Low;
        else if (Take(ref text, @"\s!{1,3}(?=\s)", out _)) priority = TaskPriority.High;

        // Relative: "in 20 min", "in 2 hours", "in 3 days"
        if (Take(ref text, @"\sin\s+(\d{1,3})\s*(m|min|mins|minute|minutes|h|hr|hrs|hour|hours|d|day|days)\b", out var rel))
        {
            int n = int.Parse(rel.Groups[1].Value);
            var unit = rel.Groups[2].Value.ToLowerInvariant();
            if (unit.StartsWith("d")) date = now.Date.AddDays(n);
            else
            {
                var at = unit.StartsWith("h") ? now.AddHours(n) : now.AddMinutes(n);
                at = new DateTime(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0);
                return new Result(Clean(text), at, true, priority);
            }
        }

        // Day words
        if (Take(ref text, @"\s(today|tonight)\b", out var tw))
        {
            date = now.Date;
            if (tw.Groups[1].Value.Equals("tonight", StringComparison.OrdinalIgnoreCase)) time ??= new TimeSpan(20, 0, 0);
        }
        else if (Take(ref text, @"\s(tomorrow|tmr|tmrw)\b", out _)) date = now.Date.AddDays(1);
        else if (Take(ref text, @"\s(?:on\s+|next\s+)?(sun|mon|tue|tues|wed|thu|thur|thurs|fri|sat)(?:day|nesday|rsday|urday|sday)?\b", out var dw))
        {
            var key = dw.Groups[1].Value.ToLowerInvariant()[..3];
            int target = Array.FindIndex(DayNames, d => d.StartsWith(key));
            int diff = (target - (int)now.DayOfWeek + 7) % 7;
            date = now.Date.AddDays(diff == 0 ? 7 : diff); // "friday" on a Friday means next week
        }
        else if (Take(ref text, @"\s(?:on\s+)?(\d{1,2})\s+(jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\b", out var dm))
        {
            int day = int.Parse(dm.Groups[1].Value);
            int month = DateTime.ParseExact(dm.Groups[2].Value[..3], "MMM", CultureInfo.InvariantCulture).Month;
            var d = SafeDate(now.Year, month, day);
            if (d is { } dd && dd < now.Date) d = SafeDate(now.Year + 1, month, day);
            date = d;
        }

        // Times: "at 3pm", "3:30 pm", "15:00", "noon", "midnight"
        if (Take(ref text, @"\s(?:at\s+)?(noon|midday)\b", out _)) time = new TimeSpan(12, 0, 0);
        else if (Take(ref text, @"\s(?:at\s+)?midnight\b", out _)) time = new TimeSpan(0, 0, 0);
        else if (Take(ref text, @"\s(?:at\s+|@)?(\d{1,2})(?::(\d{2}))?\s*(am|pm|a\.m\.|p\.m\.)(?=\s)", out var t12))
        {
            int h = int.Parse(t12.Groups[1].Value) % 12;
            int m = t12.Groups[2].Success ? int.Parse(t12.Groups[2].Value) : 0;
            if (t12.Groups[3].Value.StartsWith("p", StringComparison.OrdinalIgnoreCase)) h += 12;
            if (h < 24 && m < 60) time = new TimeSpan(h, m, 0);
        }
        else if (Take(ref text, @"\s(?:at\s+|@)?([01]?\d|2[0-3]):([0-5]\d)(?=\s)", out var t24))
            time = new TimeSpan(int.Parse(t24.Groups[1].Value), int.Parse(t24.Groups[2].Value), 0);

        if (time is { } tt)
        {
            var due = (date ?? now.Date) + tt;
            if (date == null && due <= now) due = due.AddDays(1); // "3pm" after 3pm means tomorrow
            return new Result(Clean(text), due, true, priority);
        }
        return new Result(Clean(text), date, false, priority);
    }

    /// <summary>Parses just a time of day such as "3pm", "3:30 PM" or "15:00".</summary>
    public static TimeSpan? ParseTime(string s)
    {
        var r = Parse("x " + s, new DateTime(2000, 1, 1, 0, 0, 0));
        return r.HasTime && r.Due is { } d ? d.TimeOfDay : null;
    }

    private static bool Take(ref string text, string pattern, out Match match)
    {
        match = Regex.Match(text, pattern, Opt);
        if (!match.Success) return false;
        text = text.Remove(match.Index, match.Length).Insert(match.Index, " ");
        return true;
    }

    private static string Clean(string text)
    {
        var t = Regex.Replace(text, @"\s+", " ").Trim();
        t = Regex.Replace(t, @"\s+(at|on|by|for)$", "", Opt).Trim();   // dangling "at", "on"…
        return t.Length == 0 ? "Untitled task" : t;
    }

    private static DateTime? SafeDate(int y, int m, int d) =>
        d >= 1 && d <= DateTime.DaysInMonth(y, m) ? new DateTime(y, m, d) : null;
}
