using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace CluckWise;

public partial class SettingsPage : UserControl
{
    private static readonly int[] SnoozeChoices = { 1, 3, 5, 9, 10, 15, 20, 30 };
    private bool _loading = true;
    private bool _playing;

    private List<ZoneChoice> _zones = new();

    public SettingsPage()
    {
        InitializeComponent();
        var data = App.Current.Data;
        foreach (var s in Sounds.BuiltIn) SoundBox.Items.Add(s);
        foreach (var n in SnoozeChoices) SnoozeBox.Items.Add($"{n} min");
        SoundBox.SelectedItem = Sounds.BuiltIn.Contains(data.DefaultSound) ? data.DefaultSound : Sounds.RoosterCrow;
        SnoozeBox.SelectedItem = $"{data.DefaultSnoozeMinutes} min";
        if (SnoozeBox.SelectedIndex < 0) SnoozeBox.SelectedItem = "9 min";
        var v = typeof(App).Assembly.GetName().Version;
        VersionText.Text = $"CluckWise {v?.Major}.{v?.Minor}.{v?.Build}";
        TodoSwitch.IsChecked = data.ShowTodoAfterMorningAlarm;
        Clock24Switch.IsChecked = data.Use24Hour;
        DarkSwitch.IsChecked = data.DarkMode;
        _zones = CityZones.All();
        FilterZones();
        ClockList.ItemsSource = data.WorldClocks;
        _loading = false;
        IsVisibleChanged += (_, _) => { if (IsVisible) Refresh(); else StopPreview(); };
    }

    private record GroupRow(string Name, string CountText, bool CanDelete);

    private void RefreshGroups()
    {
        var data = App.Current.Data;
        GroupList.ItemsSource = Groups.All(data).Select(g =>
        {
            int n = Groups.AlarmCount(data, g);
            return new GroupRow(g, n == 1 ? "1 alarm" : $"{n} alarms", Groups.CanDelete(g));
        }).ToList();
    }

    private void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is string g && Groups.ConfirmAndDelete(App.Current.Data, g)) RefreshGroups();
    }

    public void Refresh()
    {
        RefreshGroups();
        WidgetSwitch.IsChecked = App.Current.WidgetVisible;
        WakeStatusText.Text = WakeTimer.ArmedFor is { } at
            ? $"Your PC is set to wake at {at:dddd} {TimeFormat.Time(at)} for the next alarm"
            : "No wake-up scheduled (no upcoming alarm has \"Wake the PC\" on)";
        try { StartupSwitch.IsChecked = WindowsStartup.IsEnabled(); } catch { StartupSwitch.IsChecked = false; }
    }

    private void Startup_Click(object sender, RoutedEventArgs e)
    {
        App.Current.SetStartWithWindows(StartupSwitch.IsChecked == true);
        Refresh();
    }

    private void Widget_Click(object sender, RoutedEventArgs e) =>
        App.Current.SetWidgetVisible(WidgetSwitch.IsChecked == true);

    private void Dark_Click(object sender, RoutedEventArgs e)
    {
        App.Current.Data.DarkMode = DarkSwitch.IsChecked == true;
        Theme.Apply(App.Current.Data.DarkMode);
        App.Current.SaveData();
    }

    private void Clock24_Click(object sender, RoutedEventArgs e)
    {
        var data = App.Current.Data;
        data.Use24Hour = TimeFormat.Use24Hour = Clock24Switch.IsChecked == true;
        foreach (var a in data.Alarms) a.Refresh();
        foreach (var c in data.WorldClocks) c.Refresh(DateTime.Now);
        App.Current.SaveData();
        Refresh();
    }

    private void Todo_Click(object sender, RoutedEventArgs e)
    {
        App.Current.Data.ShowTodoAfterMorningAlarm = TodoSwitch.IsChecked == true;
        App.Current.SaveData();
    }

    private void Sound_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || SoundBox.SelectedItem is not string s) return;
        App.Current.Data.DefaultSound = s;
        App.Current.SaveData();
    }

    private void Snooze_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || SnoozeBox.SelectedItem is not string s) return;
        App.Current.Data.DefaultSnoozeMinutes = int.Parse(s.Split(' ')[0]);
        App.Current.SaveData();
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_playing) { StopPreview(); return; }
        if (SoundBox.SelectedItem is not string s) return;
        Sounds.Play(s, 80, gentle: false, loop: false);
        _playing = true;
        PlayButton.Content = "Stop";
    }

    private void StopPreview()
    {
        if (!_playing) return;
        Sounds.Stop();
        _playing = false;
        PlayButton.Content = "Play";
    }

    private void FilterZones()
    {
        var q = ZoneSearch.Text.Trim();
        ZoneHint.Visibility = q.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ZoneList.ItemsSource = _zones.Where(z => CityZones.Matches(z, q)).Take(80).ToList();
    }

    private void ZoneSearch_TextChanged(object sender, TextChangedEventArgs e) => FilterZones();

    private void ZoneList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ZoneList.SelectedItem is ZoneChoice c && ClockLabelBox.Text.Trim().Length == 0)
            ClockLabelBox.Text = c.Label;
    }

    private void AddClock_Click(object sender, RoutedEventArgs e)
    {
        var choice = ZoneList.SelectedItem as ZoneChoice ?? (ZoneList.Items.Count == 1 ? ZoneList.Items[0] as ZoneChoice : null);
        if (choice == null)
        {
            ClockError.Text = "Search for a city, then click it in the list.";
            ClockError.Visibility = Visibility.Visible;
            return;
        }
        ClockError.Visibility = Visibility.Collapsed;
        var label = ClockLabelBox.Text.Trim();
        var clock = new WorldClock { TimeZoneId = choice.ZoneId, Label = label.Length > 0 ? label : choice.Label };
        clock.Refresh(DateTime.Now);
        App.Current.Data.WorldClocks.Add(clock);
        App.Current.SaveData();
        ClockLabelBox.Clear();
        ZoneSearch.Clear();
        ZoneList.SelectedIndex = -1;
    }

    private void Clock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Let the Remove button work normally.
        if (e.OriginalSource is DependencyObject d && FindAncestor<Button>(d) != null) { DragReorder.Cancel(); return; }
        DragReorder.MouseDown(sender, e);
    }
    private void Clock_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) => DragReorder.MouseMove(sender, e);
    private void Clock_DragOver(object sender, DragEventArgs e) => DragReorder.DragOver(sender, e);
    private void Clock_DragLeave(object sender, DragEventArgs e) => DragReorder.DragLeave(sender, e);
    private void Clock_Drop(object sender, DragEventArgs e)
    {
        if (DragReorder.Drop(sender, e)) App.Current.SaveData();
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        for (; d != null; d = d is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is T t) return t;
        return null;
    }

    private void RemoveClock_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is WorldClock c) App.Current.Data.WorldClocks.Remove(c);
        App.Current.SaveData();
    }

    private void Power_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("control.exe", "powercfg.cpl") { UseShellExecute = true })?.Dispose();

    private void OpenData_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Storage.Folder);
        Process.Start(new ProcessStartInfo(Storage.Folder) { UseShellExecute = true })?.Dispose();
    }
}
