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
        ((System.Windows.Data.ListCollectionView)view).CustomSort = new AlarmSorter(App.Current.Data);
        ApplySortMode();
        SortBox.SelectedIndex = App.Current.Data.AlarmSort == "Groups" ? 1 : 0;
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
        ResortIfDue(now);
        if (NavTasks != null) UpdateTaskCount();
        foreach (var c in App.Current.Data.WorldClocks) c.Refresh(now);
        DateText.Text = now.ToString("dddd, d MMMM yyyy");
        var next = Scheduler.NextAlarm(App.Current.Data.Alarms.Where(a => !a.IsNew), now);
        _next = next;
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
        // "Skip once" only makes sense for a normal upcoming ring (not a snooze or a quick nap).
        SkipOnceButton.Visibility = next is { } nn && nn.alarm.SnoozedUntil == null && !nn.alarm.DeleteAfterRing
            ? Visibility.Visible : Visibility.Collapsed;
        UpdateVacationBanner();
    }

    private (Alarm alarm, DateTime when)? _next;

    // ---- Skip next time --------------------------------------------------------------

    private void SkipOnce_Click(object sender, RoutedEventArgs e)
    {
        if (_next is not { } n) return;
        Skip(n.alarm, n.when);
    }

    private void Skip(Alarm alarm, DateTime when)
    {
        alarm.SkipOccurrence = when;
        alarm.Refresh();
        App.Current.SaveData();
        RefreshList();
        ShowUndo($"Skipping {alarm.Label} on {when:ddd} {TimeFormat.Time(when)}", () =>
        {
            alarm.SkipOccurrence = null;
            alarm.Refresh();
            App.Current.SaveData();
            RefreshList();
        });
    }

    private static Alarm? AlarmOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as Alarm
        ?? ((sender as System.Windows.Controls.MenuItem)?.Parent as System.Windows.Controls.ContextMenu)?.DataContext as Alarm;

    /// <summary>Sets the menu text to "Skip next time (Mon 6:30 AM)" or "Undo skip".</summary>
    private void Card_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not Alarm a || fe.ContextMenu?.Items[0] is not System.Windows.Controls.MenuItem skip) return;
        if (a.SkipOccurrence != null) skip.Header = "Undo skip (" + a.SkipText + ")";
        else if (a.Enabled && Scheduler.NextOccurrence(a, DateTime.Now) is { } n) skip.Header = $"Skip next time ({n:ddd} {TimeFormat.Time(n)})";
        else skip.Header = "Skip next time";
        skip.IsEnabled = a.SkipOccurrence != null || (a.Enabled && Scheduler.NextOccurrence(a, DateTime.Now) != null);
    }

    private void CardSkip_Click(object sender, RoutedEventArgs e)
    {
        if (AlarmOf(sender) is not { } a) return;
        if (a.SkipOccurrence != null)
        {
            a.SkipOccurrence = null;
            a.Refresh();
            App.Current.SaveData();
            RefreshList();
        }
        else if (Scheduler.NextOccurrence(a, DateTime.Now) is { } n) Skip(a, n);
    }

    private void CardRingNow_Click(object sender, RoutedEventArgs e)
    {
        if (AlarmOf(sender) is not { } a) return;
        var test = a.Clone();
        test.Label += " (test)";
        test.Actions = new();
        new RingWindow(test) { Owner = this }.Show();
    }

    private void CardDuplicate_Click(object sender, RoutedEventArgs e)
    {
        if (AlarmOf(sender) is not { } a) return;
        var copy = a.Clone();
        copy.Id = Guid.NewGuid();
        copy.Label = a.Label + " (copy)";
        copy.LastRung = null;
        copy.SkipOccurrence = null;
        App.Current.Data.Alarms.Add(copy);
        App.Current.SaveData();
        RefreshList();
        Expand(copy);
    }

    // ---- Quick nap -------------------------------------------------------------------

    private void Nap_Click(object sender, RoutedEventArgs e)
    {
        var menu = NapButton.ContextMenu;
        menu.PlacementTarget = NapButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void NapItem_Click(object sender, RoutedEventArgs e) =>
        App.Current.QuickNap(int.Parse((string)((System.Windows.Controls.MenuItem)sender).Tag));

    // ---- Vacation mode ---------------------------------------------------------------

    private void Vacation_Click(object sender, RoutedEventArgs e)
    {
        var data = App.Current.Data;
        VacationGroupBox.Items.Clear();
        VacationGroupBox.Items.Add("All alarms");
        foreach (var g in Groups.All(data)) VacationGroupBox.Items.Add("Only " + g);
        VacationGroupBox.SelectedIndex = 0;
        VacationDate.SelectedDate = data.VacationUntil?.Date ?? DateTime.Today.AddDays(7);
        VacationDate.DisplayDateStart = DateTime.Today.AddDays(1);
        VacationError.Visibility = Visibility.Collapsed;
        VacationForm.Visibility = Visibility.Visible;
    }

    private void VacationStart_Click(object sender, RoutedEventArgs e)
    {
        if (VacationDate.SelectedDate is not { } back || back.Date <= DateTime.Today)
        {
            VacationError.Text = "Pick the day you're back (tomorrow or later).";
            VacationError.Visibility = Visibility.Visible;
            return;
        }
        var data = App.Current.Data;
        data.VacationUntil = back.Date;
        data.VacationGroup = VacationGroupBox.SelectedIndex <= 0 ? null : ((string)VacationGroupBox.SelectedItem)["Only ".Length..];
        Scheduler.ApplyVacation(data);
        App.Current.SaveData();
        VacationForm.Visibility = Visibility.Collapsed;
        RefreshList();
        WakeTimer.Update(data);
    }

    private void VacationCancel_Click(object sender, RoutedEventArgs e) => VacationForm.Visibility = Visibility.Collapsed;

    private void VacationEnd_Click(object sender, RoutedEventArgs e)
    {
        var data = App.Current.Data;
        data.VacationUntil = null;
        data.VacationGroup = null;
        Scheduler.ApplyVacation(data);
        App.Current.SaveData();
        RefreshList();
        WakeTimer.Update(data);
    }

    private void UpdateVacationBanner()
    {
        var data = App.Current.Data;
        if (data.VacationUntil is { } back)
        {
            var which = data.VacationGroup == null ? "All alarms are" : $"\u201c{data.VacationGroup}\u201d alarms are";
            VacationText.Text = $"Vacation mode: {which} paused until you're back on {back:dddd d MMMM}.";
            VacationBanner.Visibility = Visibility.Visible;
        }
        else VacationBanner.Visibility = Visibility.Collapsed;
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
        if (AlarmsPage == null || TimersView == null || StopwatchView == null || SettingsView == null || MorningView == null || BirthdaysView == null || TasksView == null || CalendarView == null || DeepWorkView == null) return; // still loading
        DeepWorkView.Visibility = sender == NavDeepWork ? Visibility.Visible : Visibility.Collapsed;
        CalendarView.Visibility = sender == NavCalendar ? Visibility.Visible : Visibility.Collapsed;
        TasksView.Visibility = sender == NavTasks ? Visibility.Visible : Visibility.Collapsed;
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

        // Keep groups that have no alarms yet (created with "+ New group") after the visible ones.
        order.AddRange(App.Current.Data.GroupOrder.Where(g => !order.Contains(g, StringComparer.OrdinalIgnoreCase)));
        App.Current.Data.GroupOrder = order;
        App.Current.SaveData();
        view.Refresh();
    }

    public void RefreshWidgetSwitch() => SettingsView?.Refresh();

    private bool _sortReady;
    private int _lastSortMinute = -1;

    /// <summary>Groups the list only in "By group" mode; "Soonest first" is one flat list.</summary>
    private void ApplySortMode()
    {
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(AlarmList.ItemsSource);
        view.GroupDescriptions.Clear();
        if (App.Current.Data.AlarmSort == "Groups")
            view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(Alarm.Group)));
        view.Refresh();
    }

    private void SortBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_sortReady) { _sortReady = true; return; } // ignore the initial selection
        if (SortBox.SelectedItem is not System.Windows.Controls.ComboBoxItem item) return;
        App.Current.Data.AlarmSort = (string)item.Tag;
        App.Current.SaveData();
        ApplySortMode();
    }

    /// <summary>Re-sorts once a minute (countdowns change order), but never while an alarm card is open.</summary>
    private void ResortIfDue(DateTime now)
    {
        if (now.Minute == _lastSortMinute) return;
        if (App.Current.Data.Alarms.Any(a => a.IsExpanded)) return;
        _lastSortMinute = now.Minute;
        System.Windows.Data.CollectionViewSource.GetDefaultView(AlarmList.ItemsSource).Refresh();
    }

    public void ShowTasks(bool focusQuickAdd)
    {
        NavTasks.IsChecked = true;
        if (focusQuickAdd) Dispatcher.BeginInvoke(new Action(() => TasksView.FocusQuickAdd()), System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>"Tasks · 3" in the sidebar: open tasks due today or overdue.</summary>
    public void UpdateTaskCount()
    {
        int n = App.Current.Data.TodoItems.Count(t => !t.Done && t.Section is "Today" or "Overdue");
        NavTasks.Content = n > 0 ? $"Tasks · {n}" : "Tasks";
    }

    public void RefreshList()
    {
        System.Windows.Data.CollectionViewSource.GetDefaultView(AlarmList.ItemsSource).Refresh();
        UpdateNext();
    }

    private void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is System.Windows.Data.CollectionViewGroup g)
            Groups.ConfirmAndDelete(App.Current.Data, g.Name?.ToString() ?? "");
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
