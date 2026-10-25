using CluckWise;
using Xunit;

namespace CluckWise.Tests;

public class MorningTests
{
    private static Alarm Morning(int hour = 6) => new() { Hour = hour, Minute = 30 };

    [Fact]
    public void Streak_GrowsOncePerDay_AndWeekendGapsDontBreakIt()
    {
        var data = new AppData();
        var a = Morning();
        CluckWise.Morning.RecordDismiss(data, a, new DateTime(2026, 10, 30)); // Fri
        CluckWise.Morning.RecordDismiss(data, a, new DateTime(2026, 10, 30)); // same day again
        CluckWise.Morning.RecordDismiss(data, a, new DateTime(2026, 11, 2));  // Mon
        Assert.Equal(2, data.Streak);
        Assert.Equal(2, data.BestStreak);
    }

    [Fact]
    public void Snoozing_ResetsTheStreak_ButKeepsTheBest()
    {
        var data = new AppData();
        var a = Morning();
        CluckWise.Morning.RecordDismiss(data, a, new DateTime(2026, 11, 2));
        CluckWise.Morning.RecordDismiss(data, a, new DateTime(2026, 11, 3));
        a.SnoozedThisRing = true;
        CluckWise.Morning.RecordDismiss(data, a, new DateTime(2026, 11, 4));
        Assert.Equal(0, data.Streak);
        Assert.Equal(2, data.BestStreak);
        Assert.False(a.SnoozedThisRing);
    }

    [Fact]
    public void AfternoonAlarms_DontAffectTheStreak()
    {
        var data = new AppData();
        CluckWise.Morning.RecordDismiss(data, Morning(hour: 14), new DateTime(2026, 11, 2));
        Assert.Equal(0, data.Streak);
    }
}

public class BirthdayTests
{
    [Fact]
    public void LeapDayBirthday_IsOn28FebruaryInOtherYears()
    {
        var b = new Birthday { Month = 2, Day = 29 };
        Assert.Equal(new DateTime(2027, 2, 28), b.Next(new DateTime(2027, 1, 10)));
        Assert.Equal(new DateTime(2028, 2, 29), b.Next(new DateTime(2028, 1, 10)));
    }

    [Fact]
    public void BirthdayEarlierThisYear_IsNextYear_AndTurningAgeCountsIt()
    {
        var b = new Birthday { Month = 3, Day = 14, Year = 1996 };
        var today = new DateTime(2026, 10, 26);
        Assert.Equal(new DateTime(2027, 3, 14), b.Next(today));
        Assert.Equal(31, b.TurningAge(today));
    }

    [Fact]
    public void BirthdayToday_IsToday()
    {
        var b = new Birthday { Month = 10, Day = 26 };
        Assert.Equal(new DateTime(2026, 10, 26), b.Next(new DateTime(2026, 10, 26)));
    }
}

public class TimeFormatTests
{
    [Fact]
    public void TwelveAndTwentyFourHourFormats()
    {
        try
        {
            TimeFormat.Use24Hour = false;
            Assert.Equal("6:30", TimeFormat.Digits(6, 30));
            Assert.Equal("PM", TimeFormat.Suffix(18));
            TimeFormat.Use24Hour = true;
            Assert.Equal("18:05", TimeFormat.Digits(18, 5));
            Assert.Equal("", TimeFormat.Suffix(18));
        }
        finally { TimeFormat.Use24Hour = false; }
    }
}
