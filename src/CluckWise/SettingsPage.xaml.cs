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
        foreach (var z in TimeZoneInfo.GetSystemTimeZones()) ZoneBox.Items.Add(new ZoneItem(z));
        ClockList.ItemsSource = data.WorldClocks;
        _loading = false;
        IsVisibleChanged += (_, _) => { if (IsVisible) Refresh(); else StopPreview(); };
    }

    public void Refresh()
    {
        WakeStatusText.Text = WakeTimer.ArmedFor is { } at
            ? $"Your PC is set to wake at {at:dddd h:mm tt} for the next alarm"
            : "No wake-up scheduled (no upcoming alarm has \"Wake the PC\" on)";
        try { StartupSwitch.IsChecked = WindowsStartup.IsEnabled(); } catch { StartupSwitch.IsChecked = false; }
    }

    private void Startup_Click(object sender, RoutedEventArgs e)
    {
        App.Current.SetStartWithWindows(StartupSwitch.IsChecked == true);
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

    private record ZoneItem(TimeZoneInfo Zone)
    {
        public override string ToString() => Zone.DisplayName;
    }

    private void AddClock_Click(object sender, RoutedEventArgs e)
    {
        var item = ZoneBox.SelectedItem as ZoneItem
                   ?? ZoneBox.Items.OfType<ZoneItem>().FirstOrDefault(z => z.Zone.DisplayName.Contains(ZoneBox.Text.Trim(), StringComparison.OrdinalIgnoreCase));
        if (item == null || ZoneBox.Text.Trim().Length == 0)
        {
            MessageBox.Show(Window.GetWindow(this), "Pick a time zone from the list first.", "CluckWise");
            return;
        }
        var label = ClockLabelBox.Text.Trim();
        var clock = new WorldClock { TimeZoneId = item.Zone.Id, Label = label.Length > 0 ? label : WorldClock.DefaultLabel(item.Zone) };
        clock.Refresh(DateTime.Now);
        App.Current.Data.WorldClocks.Add(clock);
        App.Current.SaveData();
        ClockLabelBox.Clear();
        ZoneBox.SelectedIndex = -1;
        ZoneBox.Text = "";
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
