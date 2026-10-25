using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace CluckWise;

public partial class TasksPage : UserControl
{
    private readonly ListCollectionView _view;
    private int _lastMinute = -1;

    public TasksPage()
    {
        InitializeComponent();
        var items = App.Current.Data.TodoItems;
        _view = (ListCollectionView)CollectionViewSource.GetDefaultView(items);
        _view.CustomSort = new TaskSorter();
        _view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TaskItem.Section)));
        TaskList.ItemsSource = _view;
        PriorityBox.SelectedIndex = 0;
        items.CollectionChanged += (_, _) => Refresh();
        IsVisibleChanged += (_, _) => { if (IsVisible) Refresh(); };
        App.Current.Scheduler.Ticked += () =>
        {
            // Sections change as time passes (Today → Overdue, midnight); re-sort once a minute.
            if (IsVisible && DateTime.Now.Minute != _lastMinute) Refresh();
        };
        Refresh();
    }

    public void FocusQuickAdd()
    {
        QuickBox.Focus();
        QuickBox.SelectAll();
    }

    /// <summary>Re-sorts and regroups, unless a task is being edited (so the editor doesn't jump).</summary>
    public void Refresh()
    {
        var items = App.Current.Data.TodoItems;
        _lastMinute = DateTime.Now.Minute;
        if (!items.Any(t => t.IsEditing))
        {
            foreach (var t in items) t.RefreshAll();
            _view.Refresh();
        }
        int today = items.Count(t => !t.Done && t.Section is "Today" or "Overdue");
        int overdue = items.Count(t => !t.Done && t.Section == "Overdue");
        SubtitleText.Text = items.Count(t => !t.Done) == 0 ? "All clear"
            : $"{today} for today" + (overdue > 0 ? $" · {overdue} overdue" : "") + $" · {items.Count(t => !t.Done)} open";
        EmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        App.Current.TasksChanged();
    }

    // ---- quick add -----------------------------------------------------------------

    private void Add()
    {
        var text = QuickBox.Text.Trim();
        if (text.Length == 0) return;
        var r = TaskParser.Parse(text, DateTime.Now);
        var priority = r.Priority != TaskPriority.Normal ? r.Priority
            : PriorityBox.SelectedIndex switch { 1 => TaskPriority.High, 2 => TaskPriority.Low, _ => TaskPriority.Normal };
        App.Current.Data.TodoItems.Add(new TaskItem { Title = r.Title, Due = r.Due, HasTime = r.HasTime, Priority = priority });
        App.Current.SaveData();
        QuickBox.Clear();
        PriorityBox.SelectedIndex = 0;
    }

    private void Add_Click(object sender, RoutedEventArgs e) => Add();

    private void QuickBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Add(); e.Handled = true; }
    }

    /// <summary>Shows what CluckWise understood, e.g. "Call Mum · Today · 3:00 PM · reminder".</summary>
    private void QuickBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = QuickBox.Text.Trim();
        QuickHint.Visibility = text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (text.Length == 0) { PreviewText.Visibility = Visibility.Collapsed; return; }
        var r = TaskParser.Parse(text, DateTime.Now);
        var parts = new List<string> { "“" + r.Title + "”" };
        if (r.Due is { } d) parts.Add(TaskItem.Friendly(d, r.HasTime) + (r.HasTime ? " (with a reminder)" : ""));
        else parts.Add("no date");
        if (r.Priority != TaskPriority.Normal) parts.Add(r.Priority + " priority");
        PreviewText.Text = string.Join("  ·  ", parts);
        PreviewText.Visibility = Visibility.Visible;
    }

    // ---- list actions --------------------------------------------------------------

    private static TaskItem? TaskOf(object sender) => (sender as FrameworkElement)?.DataContext as TaskItem;

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        App.Current.SaveData();
        Refresh();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is TaskItem t) App.Current.Data.TodoItems.Remove(t);
        App.Current.SaveData();
    }

    private void Card_Click(object sender, MouseButtonEventArgs e)
    {
        // Ignore clicks on the checkbox and delete button.
        for (var d = e.OriginalSource as DependencyObject; d != null && d != sender; d = ParentOf(d))
            if (d is ButtonBase) return;
        if (TaskOf(sender) is not { } task) return;
        bool open = !task.IsEditing;
        foreach (var t in App.Current.Data.TodoItems) t.IsEditing = false;
        task.IsEditing = open;
    }

    private static DependencyObject? ParentOf(DependencyObject d) =>
        d is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);

    // ---- inline editor -------------------------------------------------------------

    private static T? Find<T>(object sender, string name) where T : DependencyObject
    {
        // Walk up to the editor's StackPanel, then find the named field inside it.
        DependencyObject? d = sender as DependencyObject;
        while (d != null && !(d is StackPanel sp && sp.DataContext is TaskItem && LogicalTreeHelper.FindLogicalNode(sp, name) != null))
            d = ParentOf(d);
        return d == null ? null : LogicalTreeHelper.FindLogicalNode(d, name) as T;
    }

    private void EditTitle_Loaded(object sender, RoutedEventArgs e)
    {
        var box = (TextBox)sender;
        box.Focus();
        box.SelectAll();
    }

    private void EditTime_Loaded(object sender, RoutedEventArgs e)
    {
        if (TaskOf(sender) is { } t) ((TextBox)sender).Text = t.HasTime && t.Due is { } d ? TimeFormat.Time(d) : "";
    }

    private void EditPriority_Loaded(object sender, RoutedEventArgs e)
    {
        if (TaskOf(sender) is { } t) ((ComboBox)sender).SelectedIndex = (int)t.Priority;
    }

    private void ClearDate_Click(object sender, RoutedEventArgs e)
    {
        if (Find<DatePicker>(sender, "EditDate") is { } date) date.SelectedDate = null;
        if (Find<TextBox>(sender, "EditTime") is { } time) time.Clear();
    }

    private void EditCancel_Click(object sender, RoutedEventArgs e)
    {
        if (TaskOf(sender) is { } t) t.IsEditing = false;
        Refresh();
    }

    private void EditSave_Click(object sender, RoutedEventArgs e)
    {
        if (TaskOf(sender) is not { } t) return;
        var title = Find<TextBox>(sender, "EditTitle")?.Text.Trim() ?? "";
        var date = Find<DatePicker>(sender, "EditDate")?.SelectedDate;
        var timeText = Find<TextBox>(sender, "EditTime")?.Text.Trim() ?? "";
        var time = timeText.Length > 0 ? TaskParser.ParseTime(timeText) : null;

        if (title.Length > 0) t.Title = title;
        t.Notes = Find<TextBox>(sender, "EditNotes")?.Text.Trim() ?? "";
        t.Priority = (TaskPriority)Math.Max(0, Find<ComboBox>(sender, "EditPriority")?.SelectedIndex ?? 1);
        if (date == null && time != null) date = DateTime.Today;
        t.Due = date?.Date + (time ?? TimeSpan.Zero);
        t.HasTime = date != null && time != null;
        t.Reminded = false;          // a new time means a new reminder
        t.SnoozedUntil = null;
        t.IsEditing = false;
        App.Current.SaveData();
        Refresh();
    }

    /// <summary>Overdue → Today → Upcoming → Someday → Done; then by due time, then priority.</summary>
    private class TaskSorter : IComparer
    {
        public int Compare(object? x, object? y)
        {
            if (x is not TaskItem a || y is not TaskItem b) return 0;
            int s = TaskItem.SectionRank(a.Section).CompareTo(TaskItem.SectionRank(b.Section));
            if (s != 0) return s;
            if (a.Done && b.Done) return Nullable.Compare(b.DoneAt, a.DoneAt); // most recently done first
            if (a.Due != b.Due)
            {
                if (a.Due == null) return 1;
                if (b.Due == null) return -1;
                return a.Due.Value.CompareTo(b.Due.Value);
            }
            int p = b.Priority.CompareTo(a.Priority);
            return p != 0 ? p : a.Created.CompareTo(b.Created);
        }
    }
}
