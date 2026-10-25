using CluckWise;
using Xunit;

namespace CluckWise.Tests;

public class SortTests
{
    private static Alarm Daily(DateTime at, string label, string group = "Work") =>
        new() { Hour = at.Hour, Minute = at.Minute, Repeat = RepeatMode.Daily, Label = label, Group = group, Enabled = true };

    [Fact]
    public void SoonestFirst_PutsTheNextAlarmOnTop_AndOffAlarmsLast()
    {
        var now = DateTime.Now;
        var data = new AppData { AlarmSort = "Soonest" };
        var later = Daily(now.AddHours(3), "later", "Exercise");
        var soon = Daily(now.AddHours(1), "soon", "Work");
        var off = Daily(now.AddMinutes(30), "off");
        off.Enabled = false;
        var list = new List<Alarm> { off, later, soon };
        list.Sort(new AlarmSorter(data).Compare);
        Assert.Equal(new[] { "soon", "later", "off" }, list.Select(a => a.Label));
    }

    [Fact]
    public void ByGroup_KeepsGroupOrder_ThenSoonestInsideEachGroup()
    {
        var now = DateTime.Now;
        var data = new AppData { AlarmSort = "Groups" };
        data.GroupOrder.AddRange(new[] { "Work", "Exercise" });
        var gym = Daily(now.AddHours(1), "gym", "Exercise");
        var workLate = Daily(now.AddHours(4), "work-late", "Work");
        var workSoon = Daily(now.AddHours(2), "work-soon", "Work");
        var list = new List<Alarm> { gym, workLate, workSoon };
        list.Sort(new AlarmSorter(data).Compare);
        Assert.Equal(new[] { "work-soon", "work-late", "gym" }, list.Select(a => a.Label));
    }
}
