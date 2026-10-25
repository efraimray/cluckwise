using System.Windows;
using System.Windows.Controls;

namespace CluckWise;

public partial class TimersPage : UserControl
{
    public TimersPage()
    {
        InitializeComponent();
        var timers = App.Current.Data.Timers;
        TimerList.ItemsSource = timers;
        timers.CollectionChanged += (_, _) => UpdateEmpty();
        UpdateEmpty();
    }

    private void UpdateEmpty() =>
        EmptyText.Visibility = App.Current.Data.Timers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void Add(string label, int seconds)
    {
        if (seconds <= 0) { TimerError.Text = "Set a time longer than zero."; TimerError.Visibility = Visibility.Visible; return; }
        TimerError.Visibility = Visibility.Collapsed;
        var timer = new CountdownTimer { Label = label, DurationSeconds = seconds, RemainingSeconds = seconds };
        App.Current.Data.Timers.Add(timer);
        timer.Start();
        App.Current.SaveData();
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        var b = (Button)sender;
        Add((string)b.Content, int.Parse((string)b.Tag));
    }

    private void StartCustom_Click(object sender, RoutedEventArgs e)
    {
        int.TryParse(MinutesBox.Text, out var min);
        int.TryParse(SecondsBox.Text, out var sec);
        var label = string.IsNullOrWhiteSpace(LabelBox.Text) ? "Timer" : LabelBox.Text.Trim();
        Add(label, Math.Max(0, min) * 60 + Math.Max(0, sec));
    }

    private static CountdownTimer TimerOf(object sender) => (CountdownTimer)((FrameworkElement)sender).Tag;

    private void StartPause_Click(object sender, RoutedEventArgs e)
    {
        var t = TimerOf(sender);
        if (t.Running) t.Pause(); else t.Start();
        App.Current.SaveData();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        TimerOf(sender).Reset();
        App.Current.SaveData();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        App.Current.Data.Timers.Remove(TimerOf(sender));
        App.Current.SaveData();
    }
}
