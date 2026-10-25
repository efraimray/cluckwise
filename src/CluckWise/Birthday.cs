using System.Text.Json.Serialization;

namespace CluckWise;

/// <summary>A yearly birthday. The year is optional; with it, CluckWise shows the age they're turning.</summary>
public class Birthday : Observable
{
    private string _countdown = "";

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public int Month { get; set; } = 1;
    public int Day { get; set; } = 1;
    public int? Year { get; set; }

    /// <summary>The year the celebration popup was last shown, so it shows once per year.</summary>
    public int LastCelebratedYear { get; set; }
    public int LastHeadsUpYear { get; set; }

    [JsonIgnore] public string DateText => new DateTime(2000, Month, Day).ToString("d MMMM");
    [JsonIgnore] public string Countdown { get => _countdown; private set => Set(ref _countdown, value); }
    [JsonIgnore] public int DaysUntil => (Next(DateTime.Today) - DateTime.Today).Days;

    /// <summary>This year's (or next year's) birthday; 29 February falls on 28 February in other years.</summary>
    public DateTime Next(DateTime today)
    {
        DateTime On(int year) => new(year, Month, Math.Min(Day, DateTime.DaysInMonth(year, Month)));
        var thisYear = On(today.Year);
        return thisYear >= today.Date ? thisYear : On(today.Year + 1);
    }

    public int? TurningAge(DateTime today) => Year is { } y ? Next(today).Year - y : null;

    public void Refresh(DateTime today)
    {
        int days = DaysUntil;
        var age = TurningAge(today) is { } a ? $" · turns {a}" : "";
        Countdown = days switch
        {
            0 => "Today!" + age,
            1 => "Tomorrow" + age,
            _ => $"in {days} days{age}",
        };
    }
}
