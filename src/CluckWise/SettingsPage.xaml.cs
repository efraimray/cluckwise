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

    private void Power_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("control.exe", "powercfg.cpl") { UseShellExecute = true })?.Dispose();

    private void OpenData_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Storage.Folder);
        Process.Start(new ProcessStartInfo(Storage.Folder) { UseShellExecute = true })?.Dispose();
    }
}
