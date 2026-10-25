using CluckWise;
using Xunit;

namespace CluckWise.Tests;

public class DeepWorkTests
{
    // Wednesday 28 October 2026
    private static readonly DateTime Wed = new(2026, 10, 28);

    private static Dictionary<string, int> Log(params (int daysAgo, int minutes)[] entries) =>
        entries.ToDictionary(e => Wed.AddDays(-e.daysAgo).ToString("yyyy-MM-dd"), e => e.minutes);

    [Fact]
    public void ThisWeek_CountsFromMonday()
    {
        var log = Log((0, 50), (1, 25), (2, 90), (3, 100)); // Wed, Tue, Mon, and last Sunday
        Assert.Equal(165, DeepWork.MinutesThisWeek(log, Wed));
    }

    [Fact]
    public void Streak_CountsDaysInARow()
    {
        Assert.Equal(3, DeepWork.Streak(Log((0, 25), (1, 25), (2, 25), (4, 25)), Wed));
    }

    [Fact]
    public void Streak_StillCountsYesterday_BeforeYouFocusToday()
    {
        Assert.Equal(2, DeepWork.Streak(Log((1, 25), (2, 25)), Wed));
        Assert.Equal(0, DeepWork.Streak(Log((2, 25)), Wed));
    }
}
