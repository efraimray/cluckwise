namespace CluckWise;

/// <summary>Formats times as 12-hour ("6:30 AM") or 24-hour ("06:30") depending on Settings.</summary>
public static class TimeFormat
{
    public static bool Use24Hour { get; set; }

    public static string Time(DateTime t) => t.ToString(Use24Hour ? "HH:mm" : "h:mm tt");

    /// <summary>The big clock digits without AM/PM ("6:30" or "06:30").</summary>
    public static string Digits(int hour, int minute) =>
        new DateTime(2000, 1, 1, hour, minute, 0).ToString(Use24Hour ? "HH:mm" : "h:mm");

    /// <summary>"AM"/"PM", or empty in 24-hour mode.</summary>
    public static string Suffix(int hour) => Use24Hour ? "" : hour < 12 ? "AM" : "PM";
}
