using CluckWise;
using Xunit;

namespace CluckWise.Tests;

public class TaskParserTests
{
    // Sunday 25 October 2026, 11:30 AM
    private static readonly DateTime Now = new(2026, 10, 25, 11, 30, 0);

    [Fact]
    public void TimeLaterToday_IsToday()
    {
        var r = TaskParser.Parse("Call Mum at 3pm", Now);
        Assert.Equal("Call Mum", r.Title);
        Assert.Equal(new DateTime(2026, 10, 25, 15, 0, 0), r.Due);
        Assert.True(r.HasTime);
    }

    [Fact]
    public void TimeAlreadyPassed_IsTomorrow()
    {
        var r = TaskParser.Parse("Take vitamins 9am", Now);
        Assert.Equal(new DateTime(2026, 10, 26, 9, 0, 0), r.Due);
    }

    [Fact]
    public void TomorrowWithTime_AndPriority()
    {
        var r = TaskParser.Parse("Pay rent tomorrow 9:30 am !high", Now);
        Assert.Equal("Pay rent", r.Title);
        Assert.Equal(new DateTime(2026, 10, 26, 9, 30, 0), r.Due);
        Assert.Equal(TaskPriority.High, r.Priority);
    }

    [Fact]
    public void InMinutes_IsRelativeToNow()
    {
        var r = TaskParser.Parse("Stretch in 20 min", Now);
        Assert.Equal("Stretch", r.Title);
        Assert.Equal(new DateTime(2026, 10, 25, 11, 50, 0), r.Due);
    }

    [Fact]
    public void Weekday_IsTheNextOne_AndTodaysNameMeansNextWeek()
    {
        Assert.Equal(new DateTime(2026, 10, 30), TaskParser.Parse("Gym friday", Now).Due);
        Assert.Equal(new DateTime(2026, 11, 1), TaskParser.Parse("Church on sunday", Now).Due);
    }

    [Fact]
    public void WeekdayWithTime_And24HourTimes()
    {
        var r = TaskParser.Parse("Team call on mon 14:00", Now);
        Assert.Equal("Team call", r.Title);
        Assert.Equal(new DateTime(2026, 10, 26, 14, 0, 0), r.Due);
    }

    [Fact]
    public void DayAndMonth_PastDatesGoToNextYear()
    {
        Assert.Equal(new DateTime(2026, 12, 24), TaskParser.Parse("Buy gifts by 24 dec", Now).Due);
        Assert.Equal(new DateTime(2027, 3, 14), TaskParser.Parse("Renew passport 14 March", Now).Due);
    }

    [Fact]
    public void NoDate_KeepsTheWholeTitle_AndWordsLikeMondayInsideOtherWordsAreIgnored()
    {
        var r = TaskParser.Parse("Read about money and sunsets", Now);
        Assert.Equal("Read about money and sunsets", r.Title);
        Assert.Null(r.Due);
        Assert.False(r.HasTime);
    }

    [Fact]
    public void Tonight_DefaultsTo8pm()
    {
        Assert.Equal(new DateTime(2026, 10, 25, 20, 0, 0), TaskParser.Parse("Pack bag tonight", Now).Due);
    }

    [Fact]
    public void ParseTime_ReadsTimesOfDay()
    {
        Assert.Equal(new TimeSpan(15, 30, 0), TaskParser.ParseTime("3:30 pm"));
        Assert.Equal(new TimeSpan(9, 0, 0), TaskParser.ParseTime("09:00"));
        Assert.Null(TaskParser.ParseTime("soon"));
    }

    [Fact]
    public void TaskSections_FollowTheClock()
    {
        var t = new TaskItem { Title = "x", Due = Now.AddHours(1), HasTime = true };
        Assert.Equal("Today", t.SectionAt(Now));
        Assert.Equal("Overdue", t.SectionAt(Now.AddHours(2)));
        Assert.Equal("Someday", new TaskItem().SectionAt(Now));
    }
}
