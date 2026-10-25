using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace CluckWise;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AlarmList.ItemsSource = App.Current.Data.Alarms;
        WorldClockList.ItemsSource = App.Current.Data.WorldClocks;
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(AlarmList.ItemsSource);
        view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(Alarm.Group)));
        ((System.Windows.Data.ListCollectionView)view).CustomSort = new AlarmSorter(App.Current.Data);
        App.Current.Scheduler.Ticked += UpdateNext;
        UpdateNext();
        RefreshStartupSwitch();
        Morning.Changed += () => Dispatcher.Invoke(UpdateStreak);
        App.Current.Data.Tasks.CollectionChanged += (_, _) => UpdateStreak();
        UpdateStreak();
        SizeChanged += (_, e) => Tag = e.NewSize.Width < 1150 ? "compact" : null;
    }

    private void UpdateNext()
    {
        var now = DateTime.Now;
        ClockText.Text = TimeFormat.Time(now);
        foreach (var c in App.Current.Data.WorldClocks) c.Refresh(now);
        DateText.Text = now.ToString("dddd, d MMMM yyyy");
        var next = Scheduler.NextAlarm(App.Current.Data.Alarms, now);
        if (next is { } n)
        {
            NextText.Text = $"{TimeFormat.Time(n.when)} · {n.alarm.Label}";
            NextWhen.Text = $"{n.when:dddd} · rings in {Scheduler.Humanize(n.when - now)}";
        }
        else
        {
            NextText.Text = "No alarms on";
            NextWhen.Text = "Turn on an alarm or add a new one";
        }
    }

    public void NewAlarm()
    {
        NavAlarms.IsChecked = true;
        var existing = App.Current.Data.Alarms.FirstOrDefault(a => a.IsNew);
        if (existing != null) { Expand(existing); return; }

        var now = DateTime.Now.AddMinutes(1);
        var draft = new Alarm
        {
            Hour = now.Hour, Minute = now.Minute, Label = "", Repeat = RepeatMode.Once, OnceDate = now.Date,
            Group = " New", // sorts to the top until it is saved
            Enabled = false, IsNew = true,
            Sound = App.Current.Data.DefaultSound,
            SnoozeMinutes = App.Current.Data.DefaultSnoozeMinutes,
        };
        App.Current.Data.Alarms.Add(draft);
        Expand(draft);
    }

    private void Expand(Alarm alarm)
    {
        foreach (var other in App.Current.Data.Alarms.Where(a => a != alarm && a.IsExpanded).ToList())
            Collapse(other);
        alarm.IsExpanded = true;
        AlarmList.SelectedItem = alarm;
        AlarmList.ScrollIntoView(alarm);
    }

    private void Collapse(Alarm alarm)
    {
        alarm.IsExpanded = false;
        if (alarm.IsNew) App.Current.Data.Alarms.Remove(alarm);
    }

    private void CardHeader_Click(object sender, MouseButtonEventArgs e)
    {
        // Clicking the on/off switch shouldn't open the card.
        for (var d = e.OriginalSource as DependencyObject; d != null; d = ParentOf(d))
            if (d is System.Windows.Controls.CheckBox) return;
        if (((FrameworkElement)sender).DataContext is not Alarm alarm) return;
        if (alarm.IsExpanded) Collapse(alarm); else Expand(alarm);
    }

    // Text pieces (Run) aren't visual elements, so walk up through their logical parent instead.
    private static DependencyObject? ParentOf(DependencyObject d) =>
        d is System.Windows.Media.Visual || d is System.Windows.Media.Media3D.Visual3D
            ? System.Windows.Media.VisualTreeHelper.GetParent(d)
            : LogicalTreeHelper.GetParent(d);

    public void EditorSaved(Alarm original, Alarm edited)
    {
        original.CopyFrom(edited);
        original.SnoozedUntil = null;
        original.IsNew = false;
        original.IsExpanded = false;
        System.Windows.Data.CollectionViewSource.GetDefaultView(AlarmList.ItemsSource).Refresh();
        App.Current.SaveData();
        UpdateNext();
        WakeTimer.Update(App.Current.Data);
    }

    public void EditorDeleted(Alarm alarm)
    {
        alarm.IsExpanded = false;
        App.Current.Data.Alarms.Remove(alarm);
        App.Current.SaveData();
        UpdateNext();
        var label = string.IsNullOrWhiteSpace(alarm.Label) ? "Alarm" : alarm.Label;
        ShowUndo($"\u201c{label}\u201d deleted", () =>
        {
            App.Current.Data.Alarms.Add(alarm);
            App.Current.SaveData();
            UpdateNext();
        });
    }

    private Action? _undo;
    private readonly System.Windows.Threading.DispatcherTimer _undoTimer = new() { Interval = TimeSpan.FromSeconds(6) };

    private void ShowUndo(string message, Action undo)
    {
        _undo = undo;
        UndoText.Text = message;
        UndoBar.Visibility = Visibility.Visible;
        AnimateUndo(show: true);
        _undoTimer.Stop();
        _undoTimer.Tick -= UndoTimer_Tick;
        _undoTimer.Tick += UndoTimer_Tick;
        _undoTimer.Start();
    }

    private void UndoTimer_Tick(object? sender, EventArgs e) => HideUndo();

    private void HideUndo()
    {
        _undoTimer.Stop();
        _undo = null;
        AnimateUndo(show: false);
    }

    private void AnimateUndo(bool show)
    {
        var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(200);
        var fade = new System.Windows.Media.Animation.DoubleAnimation(show ? 1 : 0, duration);
        if (!show) fade.Completed += (_, _) => { if (_undo == null) UndoBar.Visibility = Visibility.Collapsed; };
        UndoBar.BeginAnimation(OpacityProperty, fade);
        UndoSlide.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new System.Windows.Media.Animation.DoubleAnimation(show ? 0 : 20, duration) { EasingFunction = ease });
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        var undo = _undo;
        HideUndo();
        undo?.Invoke();
    }

    public void EditorCancelled(Alarm alarm) => Collapse(alarm);

    private void NewAlarm_Click(object sender, RoutedEventArgs e) => NewAlarm();

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (AlarmsPage == null || TimersView == null || StopwatchView == null || SettingsView == null || MorningView == null || BirthdaysView == null) return; // still loading
        BirthdaysView.Visibility = sender == NavBirthdays ? Visibility.Visible : Visibility.Collapsed;
        MorningView.Visibility = sender == NavMorning ? Visibility.Visible : Visibility.Collapsed;
        AlarmsPage.Visibility = sender == NavAlarms ? Visibility.Visible : Visibility.Collapsed;
        TimersView.Visibility = sender == NavTimers ? Visibility.Visible : Visibility.Collapsed;
        StopwatchView.Visibility = sender == NavStopwatch ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = sender == NavSettings ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateStreak()
    {
        var data = App.Current.Data;
        SideStreakText.Text = Morning.StreakText(data);
        int done = data.Tasks.Count(t => t.Done);
        SideTodoText.Text = data.Tasks.Count == 0 ? "Add your morning to-do" : $"To-do: {done} of {data.Tasks.Count} done";
    }

    private void StreakCard_Click(object sender, MouseButtonEventArgs e) => NavMorning.IsChecked = true;

    // Drag a world clock card onto another to rearrange them.
    private void Clock_MouseDown(object sender, MouseButtonEventArgs e) => DragReorder.MouseDown(sender, e);
    private void Clock_MouseMove(object sender, MouseEventArgs e) => DragReorder.MouseMove(sender, e);
    private void Clock_DragOver(object sender, DragEventArgs e) => DragReorder.DragOver(sender, e);
    private void Clock_DragLeave(object sender, DragEventArgs e) => DragReorder.DragLeave(sender, e);
    private void Clock_Drop(object sender, DragEventArgs e)
    {
        if (DragReorder.Drop(sender, e)) App.Current.SaveData();
    }

    // Drag a group heading (Morning, Work…) onto another to change the order of groups.
    private void Group_MouseDown(object sender, MouseButtonEventArgs e) => DragReorder.MouseDown(sender, e);
    private void Group_MouseMove(object sender, MouseEventArgs e) => DragReorder.MouseMove(sender, e);
    private void Group_DragOver(object sender, DragEventArgs e) => DragReorder.DragOver(sender, e);
    private void Group_DragLeave(object sender, DragEventArgs e) => DragReorder.DragLeave(sender, e);
    private void Group_Drop(object sender, DragEventArgs e)
    {
        DragReorder.DragLeave(sender, e);
        if (e.Data.GetData(typeof(object)) is not System.Windows.Data.CollectionViewGroup dragged) return;
        if (((FrameworkElement)sender).DataContext is not System.Windows.Data.CollectionViewGroup target || dragged == target) return;

        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(AlarmList.ItemsSource);
        var order = view.Groups.OfType<System.Windows.Data.CollectionViewGroup>()
                        .Select(g => g.Name?.ToString() ?? "").Where(n => n.Trim() != "New").ToList();
        string from = dragged.Name?.ToString() ?? "", to = target.Name?.ToString() ?? "";
        int toIndex = order.IndexOf(to);
        if (toIndex < 0 || !order.Remove(from)) return;
        order.Insert(Math.Min(toIndex, order.Count), from);

        App.Current.Data.GroupOrder = order;
        App.Current.SaveData();
        view.Refresh();
    }

    public void RefreshStartupSwitch() => SettingsView?.Refresh();

    private void Switch_Click(object sender, RoutedEventArgs e)
    {
        App.Current.SaveData();
        UpdateNext();
    }

    // Ctrl+N = new alarm, Esc = close the open alarm card,
    // on the Stopwatch page: Space = start/pause, L = lap, R = reset
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        bool typing = Keyboard.FocusedElement is System.Windows.Controls.TextBox;
        if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) { NewAlarm(); e.Handled = true; }
        else if (e.Key == Key.Escape && App.Current.Data.Alarms.FirstOrDefault(a => a.IsExpanded) is { } open) { Collapse(open); e.Handled = true; }
        else if (!typing && StopwatchView.IsVisible && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (e.Key == Key.Space) { StopwatchView.StartPause(); e.Handled = true; }
            else if (e.Key == Key.L) { StopwatchView.Lap(); e.Handled = true; }
            else if (e.Key == Key.R) { StopwatchView.Reset(); e.Handled = true; }
        }
        base.OnPreviewKeyDown(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Closing the window keeps CluckWise running in the tray so alarms still ring.
        e.Cancel = true;
        Hide();
        App.Current.OnMainHidden();
    }
}
