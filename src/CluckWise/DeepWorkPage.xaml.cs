using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CluckWise;

public partial class DeepWorkPage : UserControl
{
    private bool _loading = true;

    public DeepWorkPage()
    {
        InitializeComponent();
        var d = App.Current.Data;
        foreach (var n in new[] { 5, 10, 15, 20 }) BreakBox.Items.Add($"{n} min");
        foreach (var s in Ambient.Choices) SoundBox.Items.Add(s);
        BreakBox.SelectedItem = $"{d.BreakMinutes} min";
        if (BreakBox.SelectedIndex < 0) BreakBox.SelectedIndex = 0;
        SoundBox.SelectedItem = Ambient.Choices.Contains(d.FocusSound) ? d.FocusSound : "None";
        RepeatBox.IsChecked = d.FocusAutoRepeat;
        QuietBox.IsChecked = d.QuietDuringFocus;
        CustomBox.Text = d.FocusMinutes.ToString();
        (d.FocusMinutes switch { 25 => Len25, 50 => Len50, 90 => Len90, _ => LenCustom }).IsChecked = true;
        _loading = false;

        DeepWork.Changed += Render;
        IsVisibleChanged += (_, _) => { if (IsVisible) Render(); };
        App.Current.FocusHotkey += ToggleFromHotkey;
        Render();
    }

    private void Render()
    {
        bool running = DeepWork.Phase != FocusPhase.Idle;
        SetupView.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
        RunningView.Visibility = running ? Visibility.Visible : Visibility.Collapsed;

        var log = App.Current.Data.FocusLog;
        var today = DateTime.Today;
        TodayText.Text = Minutes(DeepWork.MinutesOn(log, today));
        WeekText.Text = Minutes(DeepWork.MinutesThisWeek(log, today));
        int streak = DeepWork.Streak(log, today);
        StreakText.Text = streak == 1 ? "1 day" : $"{streak} days";
        if (!running) return;

        bool focus = DeepWork.Phase == FocusPhase.Focus;
        PhaseText.Text = DeepWork.Paused ? "PAUSED" : focus ? "FOCUS" : "BREAK";
        RunTaskText.Text = focus ? DeepWork.Task : "Stretch, drink water, look away from the screen";
        var left = DeepWork.Remaining;
        RemainText.Text = $"{(int)left.TotalMinutes:00}:{left.Seconds:00}";
        SessionText.Text = $"Session {DeepWork.SessionsDone + (focus ? 1 : 0)} · {(int)DeepWork.PhaseLength.TotalMinutes} min";
        PauseButton.Content = DeepWork.Paused ? "Resume" : "Pause";
        SkipButton.Visibility = focus ? Visibility.Collapsed : Visibility.Visible;
        Arc.Data = ArcGeometry(DeepWork.Progress, 140, 8);
    }

    /// <summary>A circular progress arc starting at 12 o'clock.</summary>
    private static Geometry ArcGeometry(double progress, double radius, double inset)
    {
        progress = Math.Clamp(progress, 0, 0.9999);
        double r = radius - inset, c = radius;
        double angle = progress * 2 * Math.PI;
        var start = new Point(c, c - r);
        var end = new Point(c + r * Math.Sin(angle), c - r * Math.Cos(angle));
        var fig = new PathFigure { StartPoint = start, IsClosed = false };
        fig.Segments.Add(new ArcSegment(end, new Size(r, r), 0, progress > 0.5, SweepDirection.Clockwise, true));
        return new PathGeometry(new[] { fig });
    }

    private static string Minutes(int m) => m < 60 ? $"{m} min" : $"{m / 60} h {m % 60} min";

    // ---- setup ---------------------------------------------------------------------

    private void SaveSettings()
    {
        if (_loading) return;
        var d = App.Current.Data;
        d.FocusMinutes = Len25.IsChecked == true ? 25 : Len50.IsChecked == true ? 50 : Len90.IsChecked == true ? 90
                       : int.TryParse(CustomBox.Text, out var c) ? Math.Clamp(c, 1, 240) : 25;
        d.BreakMinutes = BreakBox.SelectedItem is string b ? int.Parse(b.Split(' ')[0]) : 5;
        d.FocusSound = SoundBox.SelectedItem as string ?? "None";
        d.FocusAutoRepeat = RepeatBox.IsChecked == true;
        d.QuietDuringFocus = QuietBox.IsChecked == true;
        App.Current.SaveData();
    }

    private void Len_Checked(object sender, RoutedEventArgs e)
    {
        if (CustomBox != null) CustomBox.IsEnabled = LenCustom.IsChecked == true;
        SaveSettings();
    }

    private void Custom_Changed(object sender, TextChangedEventArgs e) => SaveSettings();
    private void Settings_Changed(object sender, RoutedEventArgs e) => SaveSettings();

    private void TaskBox_TextChanged(object sender, TextChangedEventArgs e) =>
        TaskHint.Visibility = TaskBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void TaskBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Start_Click(sender, e); e.Handled = true; }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        DeepWork.Start(TaskBox.Text);
    }

    private void ToggleFromHotkey()
    {
        if (DeepWork.Phase == FocusPhase.Idle) DeepWork.Start(TaskBox.Text);
        else DeepWork.Stop();
    }

    // ---- running -------------------------------------------------------------------

    private void Pause_Click(object sender, RoutedEventArgs e) => DeepWork.TogglePause();
    private void Skip_Click(object sender, RoutedEventArgs e) => DeepWork.SkipBreak();
    private void End_Click(object sender, RoutedEventArgs e) => DeepWork.Stop();
}
