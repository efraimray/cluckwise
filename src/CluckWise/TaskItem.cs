using System.Text.Json.Serialization;

namespace CluckWise;

public enum TaskPriority { Low, Normal, High }

/// <summary>A to-do. With a due time it is also a reminder: CluckWise pops up when it's due.</summary>
public class TaskItem : Observable
{
    private string _title = "";
    private bool _done;
    private bool _isEditing;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get => _title; set { if (Set(ref _title, value)) Raise(nameof(Title)); } }
    public string Notes { get; set; } = "";
    public DateTime? Due { get; set; }
    public bool HasTime { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime? DoneAt { get; set; }
    public bool Reminded { get; set; }
    public DateTime? SnoozedUntil { get; set; }

    public bool Done
    {
        get => _done;
        set
        {
            if (!Set(ref _done, value)) return;
            DoneAt = value ? DateTime.Now : null;
            RefreshAll();
        }
    }

    [JsonIgnore] public bool IsEditing { get => _isEditing; set => Set(ref _isEditing, value); }

    /// <summary>"Overdue", "Today", "Upcoming", "Someday" or "Done", for grouping the list.</summary>
    [JsonIgnore] public string Section => SectionAt(DateTime.Now);

    public string SectionAt(DateTime now)
    {
        if (Done) return "Done";
        if (Due is not { } d) return "Someday";
        if (HasTime ? d < now : d.Date < now.Date) return "Overdue";
        return d.Date == now.Date ? "Today" : "Upcoming";
    }

    public static int SectionRank(string section) => section switch
    {
        "Overdue" => 0, "Today" => 1, "Upcoming" => 2, "Someday" => 3, _ => 4,
    };

    [JsonIgnore] public bool IsOverdue => Section == "Overdue";

    [JsonIgnore]
    public string DueText
    {
        get
        {
            if (Done) return DoneAt is { } at ? "Done " + Friendly(at, true) : "Done";
            return Due is { } d ? Friendly(d, HasTime) : "";
        }
    }

    [JsonIgnore] public string PriorityText => Priority switch { TaskPriority.High => "High", TaskPriority.Low => "Low", _ => "" };

    public static string Friendly(DateTime d, bool withTime)
    {
        var today = DateTime.Today;
        string day = (d.Date - today).Days switch
        {
            0 => "Today",
            1 => "Tomorrow",
            -1 => "Yesterday",
            > 1 and < 7 => d.ToString("dddd"),
            _ => d.Year == today.Year ? d.ToString("ddd d MMM") : d.ToString("d MMM yyyy"),
        };
        return withTime ? $"{day} · {TimeFormat.Time(d)}" : day;
    }

    public void RefreshAll()
    {
        Raise(nameof(Title));
        Raise(nameof(Section));
        Raise(nameof(IsOverdue));
        Raise(nameof(DueText));
        Raise(nameof(PriorityText));
        Raise(nameof(Priority));
    }
}
