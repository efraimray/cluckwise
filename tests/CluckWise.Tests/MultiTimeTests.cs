using CluckWise;
using Xunit;

namespace CluckWise.Tests;

public class MultiTimeTests
{
    // "John's Class": 9:00 AM and 6:00 PM on weekdays
    private static Alarm Class() => new()
    {
        Label = "John's Class", Hour = 9, Minute = 0, ExtraTimes = { 18 * 60 },
        Repeat = RepeatMode.Weekly, Enabled = true,
    };

    [Fact]
    public void BeforeMorning_RingsAt9()
    {
        var next = Scheduler.NextOccurrence(Class(), new DateTime(2026, 10, 26, 7, 0, 0)); // Monday
        Assert.Equal(new DateTime(2026, 10, 26, 9, 0, 0), next);
    }

    [Fact]
    public void AfterMorningClass_RingsAt6pmTheSameDay()
    {
        var next = Scheduler.NextOccurrence(Class(), new DateTime(2026, 10, 26, 9, 0, 0));
        Assert.Equal(new DateTime(2026, 10, 26, 18, 0, 0), next);
    }

    [Fact]
    public void FridayEvening_AfterBothClasses_RingsMondayAt9()
    {
        var next = Scheduler.NextOccurrence(Class(), new DateTime(2026, 10, 30, 19, 0, 0)); // Friday
        Assert.Equal(new DateTime(2026, 11, 2, 9, 0, 0), next);
    }

    [Fact]
    public void BothTimesRing_EachOnce()
    {
        var a = Class();
        var morning = Scheduler.DueNow(a, new DateTime(2026, 10, 26, 8, 59, 59), new DateTime(2026, 10, 26, 9, 0, 0));
        Assert.Equal(new DateTime(2026, 10, 26, 9, 0, 0), morning);
        a.LastRung = morning;
        var evening = Scheduler.DueNow(a, new DateTime(2026, 10, 26, 17, 59, 59), new DateTime(2026, 10, 26, 18, 0, 0));
        Assert.Equal(new DateTime(2026, 10, 26, 18, 0, 0), evening);
    }

    [Fact]
    public void OneTimeAlarmWithTwoTimes_HasBothThatDay()
    {
        var a = Class();
        a.Repeat = RepeatMode.Once;
        a.OnceDate = new DateTime(2026, 10, 26);
        Assert.Equal(new DateTime(2026, 10, 26, 18, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2026, 10, 26, 9, 0, 0)));
        Assert.Null(Scheduler.NextOccurrence(a, new DateTime(2026, 10, 26, 18, 0, 0)));
    }
}
