using CluckWise;
using Xunit;

namespace CluckWise.Tests;

public class GroupTests
{
    private static AppData Sample()
    {
        var data = new AppData();
        data.Alarms.Add(new Alarm { Label = "Run", Group = "Gym" });
        data.Alarms.Add(new Alarm { Label = "Swim", Group = "gym" });
        data.Alarms.Add(new Alarm { Label = "Stand-up", Group = "Work" });
        data.GroupOrder.AddRange(new[] { "Work", "Gym", "Family" });
        return data;
    }

    [Fact]
    public void Deleting_MovesItsAlarmsToOthers_AndForgetsTheGroup()
    {
        var data = Sample();
        int moved = Groups.Delete(data, "Gym");
        Assert.Equal(2, moved);
        Assert.All(data.Alarms.Where(a => a.Label is "Run" or "Swim"), a => Assert.Equal(Groups.Default, a.Group));
        Assert.Equal("Work", data.Alarms.Single(a => a.Label == "Stand-up").Group);
        Assert.DoesNotContain("Gym", data.GroupOrder, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Groups.Default, data.GroupOrder);
    }

    [Fact]
    public void Others_CannotBeDeleted()
    {
        var data = Sample();
        data.Alarms.Add(new Alarm { Group = Groups.Default });
        Assert.False(Groups.CanDelete("others"));
        Assert.Equal(0, Groups.Delete(data, Groups.Default));
        Assert.Equal(Groups.Default, data.Alarms.Last().Group);
    }

    [Fact]
    public void EmptyCustomGroups_AreListed_AndCanBeDeleted()
    {
        var data = Sample();
        Assert.Equal(new[] { "Work", "Gym", "Family" }, Groups.All(data));
        Groups.Delete(data, "Family");
        Assert.DoesNotContain("Family", Groups.All(data));
    }
}
