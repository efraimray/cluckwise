using CluckWise;
using Xunit;

namespace CluckWise.Tests;

public class RepeatAndSkipTests
{
    private static Alarm At(int h, int m, RepeatMode r) => new() { Hour = h, Minute = m, Repeat = r, Enabled = true, Group = "Work" };

    // ---- Every N weeks ---------------------------------------------------------------

    [Fact]
    public void EveryTwoWeeks_SkipsTheWeekInBetween()
    {
        var a = At(9, 0, RepeatMode.Weekly);
        a.Days = new[] { false, false, false, false, false, true, false }; // Fridays
        a.IntervalWeeks = 2;
        a.OnceDate = new DateTime(2026, 10, 30); // starting Friday 30 Oct
        Assert.Equal(new DateTime(2026, 10, 30, 9, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2026, 10, 26)));
        Assert.Equal(new DateTime(2026, 11, 13, 9, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2026, 10, 30, 10, 0, 0)));
    }

    // ---- Every N days ----------------------------------------------------------------

    [Fact]
    public void EveryThreeDays_CountsFromTheStartDate()
    {
        var a = At(8, 0, RepeatMode.EveryNDays);
        a.IntervalDays = 3;
        a.OnceDate = new DateTime(2026, 10, 30);
        Assert.Equal(new DateTime(2026, 10, 30, 8, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2026, 10, 25)));
        Assert.Equal(new DateTime(2026, 11, 2, 8, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2026, 10, 30, 9, 0, 0)));
    }

    // ---- First Monday / last Friday --------------------------------------------------

    [Fact]
    public void FirstMondayOfTheMonth()
    {
        var a = At(9, 0, RepeatMode.MonthlyWeekday);
        a.NthWeek = 1;
        a.NthDay = (int)DayOfWeek.Monday;
        Assert.Equal(new DateTime(2026, 11, 2, 9, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2026, 10, 30)));
        Assert.Equal(new DateTime(2026, 12, 7, 9, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2026, 11, 2, 10, 0, 0)));
    }

    [Fact]
    public void LastFridayOfTheMonth()
    {
        Assert.Equal(new DateTime(2026, 10, 30), Scheduler.NthWeekday(2026, 10, 5, DayOfWeek.Friday));
        Assert.Equal(new DateTime(2027, 2, 26), Scheduler.NthWeekday(2027, 2, 5, DayOfWeek.Friday));
    }

    // ---- Specific dates and end dates ------------------------------------------------

    [Fact]
    public void SpecificDates_RingOnlyOnThoseDays()
    {
        var a = At(7, 0, RepeatMode.Dates);
        a.SpecificDates = new() { new DateTime(2026, 11, 20), new DateTime(2026, 11, 5) };
        Assert.Equal(new DateTime(2026, 11, 5, 7, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2026, 10, 30)));
        Assert.Equal(new DateTime(2026, 11, 20, 7, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2026, 11, 5, 8, 0, 0)));
        Assert.Null(Scheduler.NextOccurrence(a, new DateTime(2026, 11, 21)));
    }

    [Fact]
    public void EndDate_StopsARepeatingAlarm()
    {
        var a = At(7, 0, RepeatMode.Daily);
        a.EndDate = new DateTime(2026, 10, 31);
        Assert.Equal(new DateTime(2026, 10, 31, 7, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2026, 10, 30, 8, 0, 0)));
        Assert.Null(Scheduler.NextOccurrence(a, new DateTime(2026, 10, 31, 8, 0, 0)));
    }

    // ---- Skip next time --------------------------------------------------------------

    [Fact]
    public void SkipNext_JumpsOverJustThatOneRing()
    {
        var a = At(6, 30, RepeatMode.Daily);
        a.SkipOccurrence = new DateTime(2026, 10, 31, 6, 30, 0);
        Assert.Equal(new DateTime(2026, 11, 1, 6, 30, 0), Scheduler.NextOccurrence(a, new DateTime(2026, 10, 30, 22, 0, 0)));
        // and it doesn't ring at the skipped time
        Assert.Null(Scheduler.DueNow(a, new DateTime(2026, 10, 31, 6, 29, 59), new DateTime(2026, 10, 31, 6, 30, 0)));
    }

    // ---- Vacation mode ---------------------------------------------------------------

    [Fact]
    public void Vacation_SilencesAlarmsUntilYoureBack_OnlyForTheChosenGroup()
    {
        try
        {
            Scheduler.VacationUntil = new DateTime(2026, 11, 3);
            Scheduler.VacationGroup = "Work";
            var work = At(7, 0, RepeatMode.Daily);
            var gym = At(7, 0, RepeatMode.Daily);
            gym.Group = "Gym";
            var now = new DateTime(2026, 10, 30, 8, 0, 0);
            Assert.Equal(new DateTime(2026, 11, 3, 7, 0, 0), Scheduler.NextOccurrence(work, now));
            Assert.Equal(new DateTime(2026, 10, 31, 7, 0, 0), Scheduler.NextOccurrence(gym, now));
            Assert.Null(Scheduler.DueNow(work, new DateTime(2026, 10, 31, 6, 59, 59), new DateTime(2026, 10, 31, 7, 0, 0)));
        }
        finally
        {
            Scheduler.VacationUntil = null;
            Scheduler.VacationGroup = null;
        }
    }
}
