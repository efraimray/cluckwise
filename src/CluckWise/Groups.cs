namespace CluckWise;

/// <summary>Alarm groups: listing them and deleting one (its alarms move to "Others").</summary>
public static class Groups
{
    public const string Default = "Others";

    /// <summary>Every group, in the user's order: arranged groups first, then any others alphabetically.</summary>
    public static List<string> All(AppData data)
    {
        var list = new List<string>();
        void Add(string g)
        {
            if (!string.IsNullOrWhiteSpace(g) && !list.Contains(g, StringComparer.OrdinalIgnoreCase)) list.Add(g);
        }
        foreach (var g in data.GroupOrder) Add(g);
        foreach (var g in data.Alarms.Where(a => !a.IsNew).Select(a => a.Group).OrderBy(g => g, StringComparer.OrdinalIgnoreCase)) Add(g);
        return list;
    }

    public static int AlarmCount(AppData data, string group) =>
        data.Alarms.Count(a => !a.IsNew && string.Equals(a.Group, group, StringComparison.OrdinalIgnoreCase));

    public static bool CanDelete(string group) => !string.Equals(group, Default, StringComparison.OrdinalIgnoreCase);

    /// <summary>Removes the group and moves its alarms to "Others". Returns how many alarms moved.</summary>
    public static int Delete(AppData data, string group)
    {
        if (!CanDelete(group)) return 0;
        int moved = 0;
        foreach (var a in data.Alarms.Where(a => string.Equals(a.Group, group, StringComparison.OrdinalIgnoreCase)))
        {
            a.Group = Default;
            a.Refresh();
            moved++;
        }
        data.GroupOrder.RemoveAll(g => string.Equals(g, group, StringComparison.OrdinalIgnoreCase));
        if (moved > 0 && !data.GroupOrder.Contains(Default, StringComparer.OrdinalIgnoreCase))
            data.GroupOrder.Add(Default);
        return moved;
    }

    /// <summary>Asks first (saying how many alarms will move), then deletes. Returns true if deleted.</summary>
    public static bool ConfirmAndDelete(AppData data, string group)
    {
        if (!CanDelete(group)) return false;
        int count = AlarmCount(data, group);
        var message = count == 0
            ? "This group has no alarms."
            : $"Its {count} alarm{(count == 1 ? "" : "s")} will move to “{Default}”. Nothing else changes.";
        if (!CluckDialog.Ask($"Delete the “{group}” group?", message, "Delete group", "Keep it")) return false;
        Delete(data, group);
        App.Current.SaveData();
        App.Current.RefreshAlarmList();
        return true;
    }
}
