namespace CluckWise;

/// <summary>One searchable entry in the world-clock picker.</summary>
public record ZoneChoice(string Label, string Display, string ZoneId)
{
    public override string ToString() => Display;
}

/// <summary>Popular cities (Windows only names a few per time zone) plus every system time zone.</summary>
public static class CityZones
{
    private static readonly (string City, string ZoneId)[] Popular =
    {
        ("New York", "Eastern Standard Time"), ("Toronto", "Eastern Standard Time"), ("Chicago", "Central Standard Time"),
        ("Denver", "Mountain Standard Time"), ("Los Angeles", "Pacific Standard Time"), ("San Francisco", "Pacific Standard Time"),
        ("Vancouver", "Pacific Standard Time"), ("Honolulu", "Hawaiian Standard Time"), ("Mexico City", "Central Standard Time (Mexico)"),
        ("São Paulo", "E. South America Standard Time"), ("Buenos Aires", "Argentina Standard Time"),
        ("London", "GMT Standard Time"), ("Dublin", "GMT Standard Time"), ("Lisbon", "GMT Standard Time"),
        ("Paris", "Romance Standard Time"), ("Madrid", "Romance Standard Time"), ("Berlin", "W. Europe Standard Time"),
        ("Rome", "W. Europe Standard Time"), ("Amsterdam", "W. Europe Standard Time"), ("Athens", "GTB Standard Time"),
        ("Istanbul", "Turkey Standard Time"), ("Moscow", "Russian Standard Time"), ("Cairo", "Egypt Standard Time"),
        ("Lagos", "W. Central Africa Standard Time"), ("Nairobi", "E. Africa Standard Time"), ("Johannesburg", "South Africa Standard Time"),
        ("Dubai", "Arabian Standard Time"), ("Riyadh", "Arab Standard Time"), ("Karachi", "Pakistan Standard Time"),
        ("Mumbai", "India Standard Time"), ("Delhi", "India Standard Time"), ("Dhaka", "Bangladesh Standard Time"),
        ("Bangkok", "SE Asia Standard Time"), ("Jakarta", "SE Asia Standard Time"), ("Ho Chi Minh City", "SE Asia Standard Time"),
        ("Manila", "Singapore Standard Time"), ("Singapore", "Singapore Standard Time"), ("Kuala Lumpur", "Singapore Standard Time"),
        ("Hong Kong", "China Standard Time"), ("Shanghai", "China Standard Time"), ("Beijing", "China Standard Time"),
        ("Taipei", "Taipei Standard Time"), ("Seoul", "Korea Standard Time"), ("Tokyo", "Tokyo Standard Time"),
        ("Perth", "W. Australia Standard Time"), ("Sydney", "AUS Eastern Standard Time"), ("Melbourne", "AUS Eastern Standard Time"),
        ("Auckland", "New Zealand Standard Time"),
    };

    public static List<ZoneChoice> All()
    {
        var list = new List<ZoneChoice>();
        foreach (var (city, id) in Popular)
        {
            try
            {
                var z = TimeZoneInfo.FindSystemTimeZoneById(id);
                list.Add(new ZoneChoice(city, $"{city} · {Offset(z)}", id));
            }
            catch { /* time zone missing on this PC */ }
        }
        foreach (var z in TimeZoneInfo.GetSystemTimeZones())
            list.Add(new ZoneChoice(WorldClock.DefaultLabel(z), z.DisplayName, z.Id));
        return list;
    }

    public static bool Matches(ZoneChoice c, string query) =>
        query.Length == 0
        || c.Display.Contains(query, StringComparison.OrdinalIgnoreCase)
        || c.ZoneId.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string Offset(TimeZoneInfo z)
    {
        var o = z.GetUtcOffset(DateTime.UtcNow);
        return $"UTC{(o < TimeSpan.Zero ? "-" : "+")}{o:hh\\:mm}";
    }
}
