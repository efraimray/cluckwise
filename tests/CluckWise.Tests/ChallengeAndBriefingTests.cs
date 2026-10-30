using CluckWise;
using Xunit;

namespace CluckWise.Tests;

public class ChallengeTests
{
    [Fact]
    public void MathsProblems_HaveTheRightAnswer_AtEveryLevel()
    {
        var rnd = new Random(1);
        for (int level = 1; level <= 3; level++)
            for (int i = 0; i < 50; i++)
            {
                var p = WakeChallenge.Maths(level, rnd);
                var parts = p.Question.Split(' ');
                int a = int.Parse(parts[0]), b = int.Parse(parts[2]);
                int expected = parts[1] == "+" ? a + b
                    : parts.Length == 5 && parts[3] == "+" ? a * b + int.Parse(parts[4])
                    : a * b - int.Parse(parts[4]);
                Assert.Equal(expected, p.Answer);
            }
    }

    [Fact]
    public void HarderLevels_AskMoreQuestions()
    {
        Assert.Equal(1, WakeChallenge.MathsCount(1));
        Assert.Equal(3, WakeChallenge.MathsCount(3));
    }

    [Fact]
    public void PhraseCheck_ForgivesCapitalsSpacesAndAFullStop_ButNotTypos()
    {
        Assert.True(WakeChallenge.PhraseMatches("Up and at it, the day is waiting", "up and at it,  the day is waiting."));
        Assert.False(WakeChallenge.PhraseMatches("Up and at it, the day is waiting", "up and at it the day is waiting"));
        Assert.False(WakeChallenge.PhraseMatches("Stretch, breathe, and get going", "Stretch, breath, and get going"));
    }

    [Fact]
    public void Tiles_AreUnique_AndTheRightCountForEachLevel()
    {
        var rnd = new Random(2);
        Assert.Equal(6, WakeChallenge.Tiles(1, rnd).Distinct().Count());
        Assert.Equal(9, WakeChallenge.Tiles(2, rnd).Distinct().Count());
        Assert.Equal(12, WakeChallenge.Tiles(3, rnd).Distinct().Count());
    }
}

public class BriefingTests
{
    [Fact]
    public void Greeting_MatchesTheTimeOfDay()
    {
        TimeFormat.Use24Hour = false;
        Assert.Equal("Good morning! It's 6:30 AM. Rise & shine.", Briefing.Greeting(new DateTime(2026, 10, 30, 6, 30, 0), "Rise & shine"));
        Assert.StartsWith("Good evening!", Briefing.Greeting(new DateTime(2026, 10, 30, 19, 0, 0), ""));
    }

    [Fact]
    public void Lines_IncludeTodaysTasks_Birthdays_AndTheNextAlarm()
    {
        TimeFormat.Use24Hour = false;
        var now = new DateTime(2026, 10, 30, 6, 31, 0);
        var data = new AppData();
        data.TodoItems.Add(new TaskItem { Title = "Team call", Due = now.Date.AddHours(10), HasTime = true });
        data.TodoItems.Add(new TaskItem { Title = "Pay rent", Due = now.Date });
        data.Birthdays.Add(new Birthday { Name = "Anna", Month = 10, Day = 30, Year = 1996 });
        data.Alarms.Add(new Alarm { Label = "Start my workday", Hour = 9, Minute = 0, Repeat = RepeatMode.Daily, Enabled = true });

        var lines = Briefing.Lines(data, now);
        Assert.Contains(lines, l => l.StartsWith("2 tasks today") && l.Contains("Team call at 10:00 AM"));
        Assert.Contains("Birthday: Anna turns 30 today", lines);
        Assert.Contains(lines, l => l.StartsWith("Next alarm: 9:00 AM · Start my workday"));
    }

    [Fact]
    public void NoPlans_MeansNoBriefing()
    {
        Assert.Empty(Briefing.Lines(new AppData(), new DateTime(2026, 10, 30, 6, 31, 0)));
        Assert.Equal("", Briefing.Spoken(new List<string>()));
    }
}
