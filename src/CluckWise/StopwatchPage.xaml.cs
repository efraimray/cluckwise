using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CluckWise;

public record Lap(string Name, string Split, string Total);

public partial class StopwatchPage : UserControl
{
    private readonly Stopwatch _watch = new();
    private readonly DispatcherTimer _ui = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private readonly ObservableCollection<Lap> _laps = new();
    private TimeSpan _lastLap = TimeSpan.Zero;

    public StopwatchPage()
    {
        InitializeComponent();
        LapList.ItemsSource = _laps;
        _ui.Tick += (_, _) => Render();
    }

    public bool IsRunning => _watch.IsRunning;

    public void StartPause()
    {
        if (_watch.IsRunning) { _watch.Stop(); _ui.Stop(); }
        else { _watch.Start(); _ui.Start(); }
        StartButton.Content = _watch.IsRunning ? "Pause" : _watch.Elapsed > TimeSpan.Zero ? "Resume" : "Start";
        LapButton.IsEnabled = _watch.IsRunning;
        Render();
    }

    public void Lap()
    {
        if (!_watch.IsRunning) return;
        var now = _watch.Elapsed;
        _laps.Insert(0, new Lap($"Lap {_laps.Count + 1}", Format(now - _lastLap), Format(now)));
        _lastLap = now;
        Render();
    }

    public void Reset()
    {
        _watch.Reset();
        _ui.Stop();
        _laps.Clear();
        _lastLap = TimeSpan.Zero;
        StartButton.Content = "Start";
        LapButton.IsEnabled = false;
        Render();
    }

    private void Render()
    {
        TimeText.Text = Format(_watch.Elapsed);
        LapNowText.Text = _laps.Count > 0 ? $"Lap {_laps.Count + 1}: {Format(_watch.Elapsed - _lastLap)}" : " ";
        NoLapsText.Visibility = _laps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string Format(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 10:00}"
                          : $"{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 10:00}";

    private void Start_Click(object sender, RoutedEventArgs e) => StartPause();
    private void Lap_Click(object sender, RoutedEventArgs e) => Lap();
    private void Reset_Click(object sender, RoutedEventArgs e) => Reset();
}
