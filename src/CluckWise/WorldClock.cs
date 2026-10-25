using System.Text.Json.Serialization;

namespace CluckWise;

/// <summary>A city clock shown on the dashboard, e.g. "Tokyo" in Tokyo Standard Time.</summary>
public class WorldClock : Observable
{
    private string _timeText = "";
    private string _dayText = "";

    public string Label { get; set; } = "";
    public string TimeZoneId { get; set; } = "UTC";

    [JsonIgnore] public string TimeText { get => _timeText; private set => Set(ref _timeText, value); }
    [JsonIgnore] public string DayText { get => _dayText; private set => Set(ref _dayText, value); }

    public void Refresh(DateTime nowLocal)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
        catch { zone = TimeZoneInfo.Utc; }
        var there = TimeZoneInfo.ConvertTime(nowLocal, TimeZoneInfo.Local, zone);
        TimeText = TimeFormat.Time(there);
        var diff = (there.Date - nowLocal.Date).Days;
        var offset = there - nowLocal;
        var hours = Math.Round(offset.TotalHours, 1);
        string rel = hours == 0 ? "same time" : (hours > 0 ? $"+{hours:0.#} h" : $"{hours:0.#} h");
        DayText = (diff switch { 1 => "Tomorrow", -1 => "Yesterday", _ => "Today" }) + " · " + rel;
    }

    /// <summary>"(UTC+09:00) Osaka, Sapporo, Tokyo" → "Osaka".</summary>
    public static string DefaultLabel(TimeZoneInfo zone)
    {
        var name = zone.DisplayName;
        var i = name.IndexOf(") ");
        if (i >= 0) name = name[(i + 2)..];
        return name.Split(',')[0].Trim();
    }
}
