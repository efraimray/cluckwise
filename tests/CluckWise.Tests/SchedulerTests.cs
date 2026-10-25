using CluckWise;
using Xunit;

// TimeFormat is static, so run test classes one after another.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CluckWise.Tests;

public class SchedulerTests
{
    private static Alarm At(int h, int m, RepeatMode repeat) => new() { Hour = h, Minute = m, Repeat = repeat, Enabled = true };

    // ---- Daily / weekly / once -------------------------------------------------------

    [Fact]
    public void Daily_LaterToday_RingsToday()
    {
        var next = Scheduler.NextOccurrence(At(18, 0, RepeatMode.Daily), new DateTime(2026, 10, 26, 9, 0, 0));
        Assert.Equal(new DateTime(2026, 10, 26, 18, 0, 0), next);
    }

    [Fact]
    public void Daily_AlreadyPassed_RingsTomorrow()
    {
        var next = Scheduler.NextOccurrence(At(6, 30, RepeatMode.Daily), new DateTime(2026, 10, 26, 9, 0, 0));
        Assert.Equal(new DateTime(2026, 10, 27, 6, 30, 0), next);
    }

    [Fact]
    public void Weekdays_FromFridayEvening_SkipsTheWeekend()
    {
        var a = At(6, 30, RepeatMode.Weekly); // default days are Mon–Fri
        var friday = new DateTime(2026, 10, 30, 20, 0, 0);
        Assert.Equal(DayOfWeek.Friday, friday.DayOfWeek);
        Assert.Equal(new DateTime(2026, 11, 2, 6, 30, 0), Scheduler.NextOccurrence(a, friday)); // Monday
    }

    [Fact]
    public void Weekly_WithNoDaysChosen_NeverRings()
    {
        var a = At(6, 30, RepeatMode.Weekly);
        a.Days = new bool[7];
        Assert.Null(Scheduler.NextOccurrence(a, new DateTime(2026, 10, 26, 0, 0, 0)));
    }

    [Fact]
    public void Once_InThePast_IsNotScheduled()
    {
        var a = At(7, 0, RepeatMode.Once);
        a.OnceDate = new DateTime(2026, 10, 1);
        Assert.Null(Scheduler.NextOccurrence(a, new DateTime(2026, 10, 26, 9, 0, 0)));
    }

    [Fact]
    public void Once_InTheFuture_RingsOnThatDate()
    {
        var a = At(7, 0, RepeatMode.Once);
        a.OnceDate = new DateTime(2026, 12, 24);
        Assert.Equal(new DateTime(2026, 12, 24, 7, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2026, 10, 26, 9, 0, 0)));
    }

    // ---- Monthly and yearly ----------------------------------------------------------

    [Fact]
    public void Monthly_On31st_UsesLastDayOfFebruary()
    {
        var a = At(9, 0, RepeatMode.Monthly);
        a.OnceDate = new DateTime(2027, 1, 31);
        Assert.Equal(new DateTime(2027, 2, 28, 9, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2027, 2, 1, 0, 0, 0)));
    }

    [Fact]
    public void Monthly_On31st_UsesThe30thInApril_ThenThe31stInMay()
    {
        var a = At(9, 0, RepeatMode.Monthly);
        a.OnceDate = new DateTime(2027, 1, 31);
        Assert.Equal(new DateTime(2027, 4, 30, 9, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2027, 4, 1, 0, 0, 0)));
        Assert.Equal(new DateTime(2027, 5, 31, 9, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2027, 4, 30, 10, 0, 0)));
    }

    [Fact]
    public void Monthly_RentOnThe1st_AfterItPassed_GoesToNextMonth()
    {
        var a = At(8, 0, RepeatMode.Monthly);
        a.OnceDate = new DateTime(2026, 1, 1);
        Assert.Equal(new DateTime(2026, 12, 1, 8, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2026, 11, 1, 9, 0, 0)));
    }

    [Fact]
    public void Yearly_On29February_RingsOn28thInNonLeapYears()
    {
        var a = At(9, 0, RepeatMode.Yearly);
        a.OnceDate = new DateTime(2024, 2, 29);
        Assert.Equal(new DateTime(2027, 2, 28, 9, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2027, 1, 1)));
        Assert.Equal(new DateTime(2028, 2, 29, 9, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2028, 1, 1)));
    }

    [Fact]
    public void Yearly_AfterThisYearsDate_RingsNextYear()
    {
        var a = At(9, 0, RepeatMode.Yearly);
        a.OnceDate = new DateTime(2020, 3, 14);
        Assert.Equal(new DateTime(2027, 3, 14, 9, 0, 0), Scheduler.NextOccurrence(a, new DateTime(2026, 6, 1)));
    }

    // ---- Ringing: missed alarms, no double rings -------------------------------------

    [Fact]
    public void DueNow_RingsWhenTheTimePassesBetweenChecks()
    {
        var a = At(6, 30, RepeatMode.Daily);
        var due = Scheduler.DueNow(a, new DateTime(2026, 10, 26, 6, 29, 59), new DateTime(2026, 10, 26, 6, 30, 0));
        Assert.Equal(new DateTime(2026, 10, 26, 6, 30, 0), due);
    }

    [Fact]
    public void DueNow_MissedWhileAsleep_LessThan30MinutesLate_StillRings()
    {
        var a = At(6, 30, RepeatMode.Daily);
        Assert.NotNull(Scheduler.DueNow(a, new DateTime(2026, 10, 26, 6, 0, 0), new DateTime(2026, 10, 26, 6, 55, 0)));
    }

    [Fact]
    public void DueNow_MoreThan30MinutesLate_DoesNotRing()
    {
        var a = At(6, 30, RepeatMode.Daily);
        Assert.Null(Scheduler.DueNow(a, new DateTime(2026, 10, 26, 6, 0, 0), new DateTime(2026, 10, 26, 7, 10, 0)));
    }

    [Fact]
    public void DueNow_DoesNotRingTwiceForTheSameTime()
    {
        var a = At(6, 30, RepeatMode.Daily);
        a.LastRung = new DateTime(2026, 10, 26, 6, 30, 0);
        Assert.Null(Scheduler.DueNow(a, new DateTime(2026, 10, 26, 6, 29, 59), new DateTime(2026, 10, 26, 6, 30, 1)));
    }

    [Fact]
    public void DueNow_DisabledOrUnsavedAlarms_NeverRing()
    {
        var off = At(6, 30, RepeatMode.Daily);
        off.Enabled = false;
        var draft = At(6, 30, RepeatMode.Daily);
        draft.IsNew = true;
        var before = new DateTime(2026, 10, 26, 6, 29, 0);
        var after = new DateTime(2026, 10, 26, 6, 31, 0);
        Assert.Null(Scheduler.DueNow(off, before, after));
        Assert.Null(Scheduler.DueNow(draft, before, after));
    }

    // ---- Snooze ----------------------------------------------------------------------

    [Fact]
    public void NextAlarm_PrefersASnoozedAlarm()
    {
        var now = new DateTime(2026, 10, 26, 6, 31, 0);
        var morning = At(7, 0, RepeatMode.Daily);
        var snoozed = At(6, 30, RepeatMode.Daily);
        snoozed.SnoozedUntil = now.AddMinutes(9);
        var next = Scheduler.NextAlarm(new[] { morning, snoozed }, now);
        Assert.Same(snoozed, next?.alarm);
        Assert.Equal(now.AddMinutes(9), next?.when);
    }

    [Fact]
    public void Countdown_WhenSnoozed_SaysSo()
    {
        TimeFormat.Use24Hour = false;
        var a = At(6, 30, RepeatMode.Daily);
        a.SnoozedUntil = new DateTime(2026, 10, 26, 6, 39, 0);
        Assert.StartsWith("Snoozed until", Scheduler.CountdownText(a, new DateTime(2026, 10, 26, 6, 31, 0)));
    }
}
