using System.Collections;

namespace CluckWise;

/// <summary>Sorts alarms by the user's group order (unsaved new alarms first), then by time.</summary>
public class AlarmSorter : IComparer
{
    private readonly AppData _data;
    public AlarmSorter(AppData data) => _data = data;

    public int Compare(object? x, object? y)
    {
        if (x is not Alarm a || y is not Alarm b) return 0;
        if (a.IsNew != b.IsNew) return a.IsNew ? -1 : 1;
        int ga = GroupRank(a.Group), gb = GroupRank(b.Group);
        if (ga != gb) return ga.CompareTo(gb);
        int byName = string.Compare(a.Group, b.Group, StringComparison.OrdinalIgnoreCase);
        if (byName != 0) return byName;
        return (a.Hour * 60 + a.Minute).CompareTo(b.Hour * 60 + b.Minute);
    }

    /// <summary>Groups the user has arranged come first, in their order; others follow alphabetically.</summary>
    private int GroupRank(string group)
    {
        int i = _data.GroupOrder.FindIndex(g => string.Equals(g, group, StringComparison.OrdinalIgnoreCase));
        return i >= 0 ? i : int.MaxValue;
    }
}
