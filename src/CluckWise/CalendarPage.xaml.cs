using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CluckWise;

/// <summary>A month view of alarms, birthdays and tasks. Click a day to see everything on it.</summary>
public partial class CalendarPage : UserControl
{
    private record Entry(DateTime? At, string Text, string Kind); // Kind: alarm | birthday | task

    private static readonly Brush AlarmBrush = Freeze(Color.FromRgb(0xF5, 0xA5, 0x24));
    private static readonly Brush BirthdayBrush = Freeze(Color.FromRgb(0xE5, 0x48, 0x4D));
    private static readonly Brush TaskBrush = Freeze(Color.FromRgb(0x3B, 0x82, 0xF6));

    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime _selected = DateTime.Today;

    public CalendarPage()
    {
        InitializeComponent();
        foreach (var d in new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" })
        {
            var t = new TextBlock { Text = d, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(6, 0, 0, 0) };
            t.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            WeekdayRow.Children.Add(t);
        }
        IsVisibleChanged += (_, _) => { if (IsVisible) Render(); };
        SizeChanged += (_, e) => DayPanel.Visibility = e.NewSize.Width < 820 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Render()
    {
        MonthText.Text = _month.ToString("MMMM yyyy");
        DayGrid.Children.Clear();
        var first = _month.AddDays(-(int)_month.DayOfWeek); // the Sunday on or before the 1st
        for (int i = 0; i < 42; i++) DayGrid.Children.Add(Cell(first.AddDays(i)));
        RenderDay();
    }

    private Border Cell(DateTime day)
    {
        var entries = EntriesOn(day);
        bool inMonth = day.Month == _month.Month, today = day == DateTime.Today, selected = day == _selected;

        var number = new TextBlock { Text = day.Day.ToString(), FontSize = 13, FontWeight = today ? FontWeights.ExtraBold : FontWeights.SemiBold };
        number.SetResourceReference(TextBlock.ForegroundProperty, today ? "Ink" : "Text");
        var badge = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 1, 6, 1), HorizontalAlignment = HorizontalAlignment.Left, Child = number };
        if (today) badge.SetResourceReference(Border.BackgroundProperty, "Yolk");

        var stack = new StackPanel { Children = { badge } };
        foreach (var e in entries.Take(3))
        {
            var line = new DockPanel { Margin = new Thickness(0, 3, 0, 0) };
            line.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = BrushFor(e.Kind), Margin = new Thickness(2, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
            var text = new TextBlock { Text = e.Text, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
            line.Children.Add(text);
            stack.Children.Add(line);
        }
        if (entries.Count > 3)
        {
            var more = new TextBlock { Text = $"+{entries.Count - 3} more", FontSize = 11, Margin = new Thickness(4, 2, 0, 0) };
            more.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            stack.Children.Add(more);
        }

        var cell = new Border
        {
            CornerRadius = new CornerRadius(12), Padding = new Thickness(6), Margin = new Thickness(3),
            BorderThickness = new Thickness(selected ? 2 : 1), Child = stack, Cursor = Cursors.Hand,
            Opacity = inMonth ? 1 : 0.45, ToolTip = day.ToString("dddd d MMMM"),
        };
        cell.SetResourceReference(Border.BackgroundProperty, "Surface");
        cell.SetResourceReference(Border.BorderBrushProperty, selected ? "Yolk" : "Line");
        cell.MouseLeftButtonUp += (_, _) => { _selected = day; if (day.Month != _month.Month) _month = new DateTime(day.Year, day.Month, 1); Render(); };
        return cell;
    }

    private void RenderDay()
    {
        var entries = EntriesOn(_selected);
        DayTitle.Text = _selected == DateTime.Today ? "Today" : _selected.ToString("dddd");
        DaySubtitle.Text = _selected.ToString("d MMMM yyyy") + (entries.Count == 0 ? "" : $" · {entries.Count} item{(entries.Count == 1 ? "" : "s")}");
        DayItems.Children.Clear();
        if (entries.Count == 0)
        {
            var none = new TextBlock { Text = "Nothing planned.", FontSize = 14 };
            none.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            DayItems.Children.Add(none);
            return;
        }
        foreach (var e in entries)
        {
            var row = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 6) };
            row.SetResourceReference(Border.BackgroundProperty, "SurfaceAlt");
            var dock = new DockPanel();
            dock.Children.Add(new Ellipse { Width = 9, Height = 9, Fill = BrushFor(e.Kind), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
            var when = new TextBlock { Text = e.At is { } at ? TimeFormat.Time(at) : KindName(e.Kind), FontSize = 12, Width = 70, VerticalAlignment = VerticalAlignment.Center };
            when.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            dock.Children.Add(when);
            dock.Children.Add(new TextBlock { Text = e.Text, FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            row.Child = dock;
            DayItems.Children.Add(row);
        }
    }

    /// <summary>Everything happening on a day, in time order (all-day items first).</summary>
    private static List<Entry> EntriesOn(DateTime day)
    {
        var data = App.Current.Data;
        var start = day.Date;
        var end = start.AddDays(1);
        var list = new List<Entry>();

        foreach (var b in data.Birthdays)
            if (b.Next(start) == start)
                list.Add(new Entry(null, b.TurningAge(start) is { } age ? $"{b.Name} turns {age}" : $"{b.Name}'s birthday", "birthday"));

        foreach (var t in data.TodoItems)
            if (!t.Done && t.Due is { } due && due.Date == start)
                list.Add(new Entry(t.HasTime ? due : null, t.Title, "task"));

        foreach (var a in data.Alarms)
        {
            if (!a.Enabled || a.IsNew) continue;
            var next = Scheduler.NextOccurrence(a, start.AddTicks(-1));
            for (int guard = 0; next is { } n && n < end && guard < 10; guard++)
            {
                list.Add(new Entry(n, a.Label, "alarm"));
                next = Scheduler.NextOccurrence(a, n);
            }
        }
        return list.OrderBy(e => e.At.HasValue).ThenBy(e => e.At).ToList();
    }

    private static Brush BrushFor(string kind) => kind switch { "birthday" => BirthdayBrush, "task" => TaskBrush, _ => AlarmBrush };
    private static string KindName(string kind) => kind switch { "birthday" => "Birthday", "task" => "Task", _ => "Alarm" };

    private static Brush Freeze(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    private void Prev_Click(object sender, RoutedEventArgs e) { _month = _month.AddMonths(-1); Render(); }
    private void Next_Click(object sender, RoutedEventArgs e) { _month = _month.AddMonths(1); Render(); }
    private void Today_Click(object sender, RoutedEventArgs e)
    {
        _selected = DateTime.Today;
        _month = new DateTime(_selected.Year, _selected.Month, 1);
        Render();
    }
}
