using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CluckWise;

public partial class AlarmEditorPanel : UserControl
{
    private Alarm _edit = new();
    private Alarm _original = new();
    private bool _initialized;
    private readonly ToggleButton[] _days = new ToggleButton[7];
    private List<AlarmAction> _actions = new();
    private bool _loading = true;
    private bool _previewing;


    public AlarmEditorPanel() => InitializeComponent();

    private MainWindow Main => (MainWindow)Window.GetWindow(this);

    private void Panel_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized || DataContext is not Alarm alarm) return;
        _initialized = true;
        Init(alarm, alarm.IsNew, App.Current.Data.GroupOrder.Concat(App.Current.Data.Alarms.Where(a => !a.IsNew).Select(a => a.Group)).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private void Init(Alarm alarm, bool isNew, IEnumerable<string> groups)
    {
        _original = alarm;
        _edit = alarm.Clone();
        _actions = _edit.Actions.ToList();
        DeleteButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;

        if (TimeFormat.Use24Hour) for (int h = 0; h < 24; h++) HourBox.Items.Add(h.ToString("00"));
        else for (int h = 1; h <= 12; h++) HourBox.Items.Add(h.ToString());
        AmPmBox.Visibility = TimeFormat.Use24Hour ? Visibility.Collapsed : Visibility.Visible;
        for (int m = 0; m < 60; m++) MinuteBox.Items.Add(m.ToString("00"));
        string[] letters = { "S", "M", "T", "W", "T", "F", "S" };
        for (int i = 0; i < 7; i++)
        {
            _days[i] = new ToggleButton { Content = letters[i], Style = (Style)FindResource("DayChip"), Margin = new Thickness(0, 0, 6, 0) };
            _days[i].Click += (_, _) => UpdateState();
            DaysPanel.Children.Add(_days[i]);
        }
        foreach (var g in groups.Concat(new[] { "Morning", "Work", "Exercise" }).Distinct()) GroupBox.Items.Add(g);
        foreach (var s in Sounds.BuiltIn) SoundBox.Items.Add(s);
        if (!Sounds.BuiltIn.Contains(_edit.Sound)) SoundBox.Items.Add(_edit.Sound);
        foreach (var n in new[] { 1, 3, 5, 9, 10, 15, 20, 30 }) SnoozeBox.Items.Add($"{n} min");
        ReminderBox.Items.Add("Off");
        foreach (var n in new[] { 5, 10, 15, 30, 60 }) ReminderBox.Items.Add($"{n} min before");

        HourBox.SelectedIndex = TimeFormat.Use24Hour ? _edit.Hour : (_edit.Hour % 12 == 0 ? 12 : _edit.Hour % 12) - 1;
        MinuteBox.SelectedIndex = _edit.Minute;
        AmPmBox.SelectedIndex = _edit.Hour < 12 ? 0 : 1;
        OnceRadio.IsChecked = _edit.Repeat == RepeatMode.Once;
        DailyRadio.IsChecked = _edit.Repeat == RepeatMode.Daily;
        WeeklyRadio.IsChecked = _edit.Repeat == RepeatMode.Weekly;
        MonthlyRadio.IsChecked = _edit.Repeat == RepeatMode.Monthly;
        YearlyRadio.IsChecked = _edit.Repeat == RepeatMode.Yearly;
        for (int i = 0; i < 7; i++) _days[i].IsChecked = _edit.Days[i];
        OnceDatePicker.SelectedDate = _edit.Repeat == RepeatMode.Once && _edit.OnceDate < DateTime.Today ? DateTime.Today : _edit.OnceDate;
        LabelBox.Text = _edit.Label;
        GroupBox.Text = isNew ? "Morning" : _edit.Group;
        SoundBox.SelectedItem = _edit.Sound;
        VolumeSlider.Value = _edit.Volume;
        GentleBox.IsChecked = _edit.GentleWake;
        SnoozeBox.SelectedItem = $"{_edit.SnoozeMinutes} min";
        if (SnoozeBox.SelectedIndex < 0) SnoozeBox.SelectedItem = "9 min";
        SilentBox.IsChecked = _edit.RunSilently;
        ReminderBox.SelectedItem = _edit.ReminderMinutes > 0 ? $"{_edit.ReminderMinutes} min before" : "Off";
        if (ReminderBox.SelectedIndex < 0) ReminderBox.SelectedIndex = 0;
        WakeBox.IsChecked = _edit.WakeComputer;
        EnabledBox.IsChecked = isNew || _edit.Enabled;
        RefreshActions();

        _loading = false;
        UpdateState();
        LabelBox.Focus();
        Unloaded += (_, _) => { if (_previewing) Sounds.Stop(); };
    }

    // Two columns when there's room; one column (sound and actions below) when the window is narrow.
    private void Panel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool narrow = e.NewSize.Width < 780;
        var cols = EditorGrid.ColumnDefinitions;
        cols[1].Width = narrow ? new GridLength(0) : new GridLength(24);
        cols[2].Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(RightColumn, narrow ? 0 : 2);
        Grid.SetRow(RightColumn, narrow ? 1 : 0);
        RightColumn.Margin = narrow ? new Thickness(0, 24, 0, 0) : new Thickness(0);
    }

    /// <summary>
    /// The actions list has its own scroll area; unless it really has more to scroll,
    /// hand the mouse wheel to the main page so scrolling never "gets stuck" here.
    /// </summary>
    private void PassWheelToPage(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        var inner = FindChild<ScrollViewer>((DependencyObject)sender);
        if (inner != null)
        {
            bool canScroll = e.Delta > 0 ? inner.VerticalOffset > 0 : inner.VerticalOffset < inner.ScrollableHeight;
            if (canScroll) return;
        }
        e.Handled = true;
        var forward = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender,
        };
        ((UIElement)((FrameworkElement)sender).Parent).RaiseEvent(forward);
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            if (FindChild<T>(child) is { } deeper) return deeper;
        }
        return null;
    }

    private void Changed(object sender, RoutedEventArgs e) => UpdateState();

    /// <summary>Copies the form into the working copy and refreshes the summary line.</summary>
    private void UpdateState()
    {
        if (_loading) return;
        if (TimeFormat.Use24Hour) _edit.Hour = Math.Max(0, HourBox.SelectedIndex);
        else
        {
            int hour12 = HourBox.SelectedIndex + 1;
            _edit.Hour = hour12 % 12 + (AmPmBox.SelectedIndex == 1 ? 12 : 0);
        }
        _edit.Minute = Math.Max(0, MinuteBox.SelectedIndex);
        _edit.Repeat = OnceRadio.IsChecked == true ? RepeatMode.Once
                     : DailyRadio.IsChecked == true ? RepeatMode.Daily
                     : MonthlyRadio.IsChecked == true ? RepeatMode.Monthly
                     : YearlyRadio.IsChecked == true ? RepeatMode.Yearly : RepeatMode.Weekly;
        _edit.Days = _days.Select(d => d.IsChecked == true).ToArray();
        _edit.OnceDate = OnceDatePicker.SelectedDate ?? DateTime.Today;

        DaysPanel.Visibility = _edit.Repeat == RepeatMode.Weekly ? Visibility.Visible : Visibility.Collapsed;
        DateRow.Visibility = _edit.Repeat is RepeatMode.Once or RepeatMode.Monthly or RepeatMode.Yearly ? Visibility.Visible : Visibility.Collapsed;
        DateHint.Text = _edit.Repeat switch
        {
            RepeatMode.Monthly => _edit.RepeatText + (_edit.OnceDate.Day > 28 ? " (last day in shorter months)" : ""),
            RepeatMode.Yearly => _edit.RepeatText,
            _ => "",
        };

        SummaryText.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var next = Scheduler.NextOccurrence(_edit, DateTime.Now);
        SummaryText.Text = next is { } n
            ? $"Rings {n:dddd d MMM} at {TimeFormat.Time(n)} · in {Scheduler.Humanize(n - DateTime.Now)}"
            : _edit.Repeat == RepeatMode.Weekly ? "Pick at least one day" : "That time has already passed";
    }

    private void NewGroup_Click(object sender, RoutedEventArgs e)
    {
        NewGroupRow.Visibility = Visibility.Visible;
        NewGroupError.Visibility = Visibility.Collapsed;
        NewGroupBox.Clear();
        NewGroupBox.Focus();
    }

    private void NewGroupCreate_Click(object sender, RoutedEventArgs e)
    {
        var name = NewGroupBox.Text.Trim();
        if (name.Length == 0) { ShowGroupError("Type a name for the group."); return; }
        var existing = GroupBox.Items.OfType<string>().FirstOrDefault(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            GroupBox.Items.Add(name);
            existing = name;
            // Remember it so it shows up for other alarms too (placed after the groups already arranged).
            var order = App.Current.Data.GroupOrder;
            if (!order.Contains(name, StringComparer.OrdinalIgnoreCase)) order.Add(name);
            App.Current.SaveData();
        }
        GroupBox.SelectedItem = existing;
        GroupBox.Text = existing;
        NewGroupRow.Visibility = Visibility.Collapsed;
    }

    private void NewGroupCancel_Click(object sender, RoutedEventArgs e) => NewGroupRow.Visibility = Visibility.Collapsed;

    private void NewGroupBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) { NewGroupCreate_Click(sender, e); e.Handled = true; }
        else if (e.Key == System.Windows.Input.Key.Escape) { NewGroupCancel_Click(sender, e); e.Handled = true; }
    }

    private void NewGroupBox_TextChanged(object sender, TextChangedEventArgs e) =>
        NewGroupHint.Visibility = NewGroupBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void ShowGroupError(string text)
    {
        NewGroupError.Text = text;
        NewGroupError.Visibility = Visibility.Visible;
    }

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        VolumeText.Text = $"{(int)VolumeSlider.Value}%";

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (_previewing) { Sounds.Stop(); _previewing = false; PreviewButton.Content = "Play"; return; }
        if (SoundBox.SelectedItem is not string sound) return;
        Sounds.Play(sound, (int)VolumeSlider.Value, gentle: false, loop: false);
        _previewing = true;
        PreviewButton.Content = "Stop";
    }

    private void BrowseSound_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Sound files|*.mp3;*.wav;*.wma;*.m4a|All files|*.*" };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        if (!SoundBox.Items.Contains(dlg.FileName)) SoundBox.Items.Add(dlg.FileName);
        SoundBox.SelectedItem = dlg.FileName;
    }

    private void AddWebsite_Click(object sender, RoutedEventArgs e)
    {
        WebsiteRow.Visibility = Visibility.Visible;
        WebsiteBox.Clear();
        HideMessage();
        WebsiteBox.Focus();
    }

    private void WebsiteAdd_Click(object sender, RoutedEventArgs e)
    {
        var url = ActionRunner.NormalizeUrl(WebsiteBox.Text);
        if (url == null) { ShowMessage("That doesn't look like a web address. Try something like news.google.com.", error: true); return; }
        _actions.Add(new AlarmAction { Kind = ActionKind.OpenWebsite, Target = url });
        RefreshActions();
        WebsiteRow.Visibility = Visibility.Collapsed;
        HideMessage();
    }

    private void WebsiteCancel_Click(object sender, RoutedEventArgs e)
    {
        WebsiteRow.Visibility = Visibility.Collapsed;
        HideMessage();
    }

    private void WebsiteBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) { WebsiteAdd_Click(sender, e); e.Handled = true; }
        else if (e.Key == System.Windows.Input.Key.Escape) { WebsiteCancel_Click(sender, e); e.Handled = true; }
    }

    private void WebsiteBox_TextChanged(object sender, TextChangedEventArgs e) =>
        WebsiteHint.Visibility = WebsiteBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>A short message under the actions: red for problems, muted for hints.</summary>
    private void ShowMessage(string text, bool error)
    {
        ActionMessage.Text = text;
        if (error) ActionMessage.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5, 0x48, 0x4D));
        else ActionMessage.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        ActionMessage.Visibility = Visibility.Visible;
    }

    private void HideMessage() => ActionMessage.Visibility = Visibility.Collapsed;

    private void AddProgram_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Choose an app or file to open", Filter = "Apps and files|*.*" };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        _actions.Add(new AlarmAction { Kind = ActionKind.OpenProgram, Target = dlg.FileName });
        RefreshActions();
    }

    private void AddMusic_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose a song or playlist to play",
            Filter = "Music and playlists|*.mp3;*.m4a;*.wav;*.wma;*.flac;*.m3u;*.m3u8;*.wpl;*.pls|All files|*.*",
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        _actions.Add(new AlarmAction { Kind = ActionKind.PlayMusic, Target = dlg.FileName });
        RefreshActions();
    }

    private void PcAction_Click(object sender, RoutedEventArgs e)
    {
        var menu = PcActionButton.ContextMenu;
        menu.PlacementTarget = PcActionButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void PcActionItem_Click(object sender, RoutedEventArgs e)
    {
        var kind = Enum.Parse<ActionKind>((string)((MenuItem)sender).Tag);
        if (_actions.Any(a => a.Kind == kind)) return;
        _actions.Add(new AlarmAction { Kind = kind });
        RefreshActions();
    }

    private void TestAction_Click(object sender, RoutedEventArgs e)
    {
        if (ActionsList.SelectedItem is not AlarmAction a) { ShowMessage("Click an action in the list first, then Test.", error: false); return; }
        var error = ActionRunner.Run(a);
        if (error != null) ShowMessage(error, error: true); else HideMessage();
    }

    private void RemoveAction_Click(object sender, RoutedEventArgs e)
    {
        if (ActionsList.SelectedItem is AlarmAction a) { _actions.Remove(a); RefreshActions(); }
    }

    // Drag an action onto another to change the order they run in.
    private void Action_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) => DragReorder.MouseDown(sender, e);
    private void Action_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) => DragReorder.MouseMove(sender, e);
    private void Action_DragOver(object sender, DragEventArgs e) => DragReorder.DragOver(sender, e);
    private void Action_DragLeave(object sender, DragEventArgs e) => DragReorder.DragLeave(sender, e);
    private void Action_Drop(object sender, DragEventArgs e)
    {
        if (DragReorder.Drop(sender, e)) RefreshActions();
    }

    private void RefreshActions()
    {
        ActionsList.ItemsSource = null;
        ActionsList.ItemsSource = _actions;
        NoActionsText.Visibility = _actions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        UpdateState();
        if (_edit.Repeat == RepeatMode.Weekly && !_edit.Days.Any(d => d))
        {
            SummaryText.Text = "Pick at least one day, or choose Once or Every day.";
            SummaryText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5, 0x48, 0x4D));
            DaysPanel.Focus();
            return;
        }
        _edit.Label = string.IsNullOrWhiteSpace(LabelBox.Text) ? "Alarm" : LabelBox.Text.Trim();
        _edit.Group = string.IsNullOrWhiteSpace(GroupBox.Text) ? "Other" : GroupBox.Text.Trim();
        _edit.Sound = SoundBox.SelectedItem as string ?? Sounds.RoosterCrow;
        _edit.Volume = (int)VolumeSlider.Value;
        _edit.GentleWake = GentleBox.IsChecked == true;
        _edit.SnoozeMinutes = int.Parse(((string)SnoozeBox.SelectedItem).Split(' ')[0]);
        _edit.RunSilently = SilentBox.IsChecked == true;
        _edit.WakeComputer = WakeBox.IsChecked == true;
        _edit.ReminderMinutes = ReminderBox.SelectedIndex <= 0 ? 0 : int.Parse(((string)ReminderBox.SelectedItem).Split(' ')[0]);
        _edit.Enabled = EnabledBox.IsChecked == true;
        _edit.Actions = _actions.ToList();
        Main.EditorSaved(_original, _edit);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        Main.EditorDeleted(_original);
    }

    private void RingNow_Click(object sender, RoutedEventArgs e)
    {
        StopPreviewIfPlaying();
        UpdateState();
        var test = _edit.Clone();
        test.Label = (string.IsNullOrWhiteSpace(LabelBox.Text) ? "Alarm" : LabelBox.Text.Trim()) + " (test)";
        test.Sound = SoundBox.SelectedItem as string ?? Sounds.RoosterCrow;
        test.Volume = (int)VolumeSlider.Value;
        test.GentleWake = GentleBox.IsChecked == true;
        test.Actions = new();
        new RingWindow(test) { Owner = Window.GetWindow(this) }.Show();
    }

    private void StopPreviewIfPlaying()
    {
        if (!_previewing) return;
        Sounds.Stop();
        _previewing = false;
        PreviewButton.Content = "Play";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Main.EditorCancelled(_original);
}
