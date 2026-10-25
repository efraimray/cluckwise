using System.Windows;
using System.Windows.Input;

namespace CluckWise;

/// <summary>A small always-on-top card showing the time, next alarm and running timer. Drag it anywhere.</summary>
public partial class FloatingWidget : Window
{
    private const double EdgeGap = 12;

    public FloatingWidget()
    {
        InitializeComponent();
        Loaded += (_, _) => { PlaceAtSavedSpot(); Update(); };
        App.Current.Scheduler.Ticked += Update;
        Closed += (_, _) => App.Current.Scheduler.Ticked -= Update;
    }

    private void Update()
    {
        var now = DateTime.Now;
        var data = App.Current.Data;
        ClockText.Text = TimeFormat.Time(now);

        if (Scheduler.NextAlarm(data.Alarms.Where(a => !a.IsNew), now) is { } n)
        {
            NextRun.Text = $"{TimeFormat.Time(n.when)} · {n.alarm.Label}";
            NextWhen.Text = "in " + Scheduler.Humanize(n.when - now);
        }
        else
        {
            NextRun.Text = "no alarms on";
            NextWhen.Text = "Double-click to add one";
        }

        FocusLine.Visibility = DeepWork.Phase == FocusPhase.Idle ? Visibility.Collapsed : Visibility.Visible;
        if (DeepWork.Phase != FocusPhase.Idle)
        {
            var left = DeepWork.Remaining;
            FocusLabel.Text = DeepWork.Paused ? "Paused " : DeepWork.Phase == FocusPhase.Focus ? "Focus " : "Break ";
            FocusRun.Text = $"{(int)left.TotalMinutes:00}:{left.Seconds:00}" + (DeepWork.Phase == FocusPhase.Focus ? " · " + DeepWork.Task : "");
        }

        var timer = data.Timers.Where(t => t.Running).OrderBy(t => t.RemainingSeconds).FirstOrDefault();
        TimerLine.Visibility = timer == null ? Visibility.Collapsed : Visibility.Visible;
        if (timer != null)
        {
            int others = data.Timers.Count(t => t.Running) - 1;
            TimerRun.Text = $"{timer.Label} · {timer.RemainingText}" + (others > 0 ? $"  (+{others})" : "");
        }
    }

    // ---- position ------------------------------------------------------------------

    private void PlaceAtSavedSpot()
    {
        var data = App.Current.Data;
        if (data.WidgetLeft is { } l && data.WidgetTop is { } t && IsOnScreen(l, t))
        {
            Left = l;
            Top = t;
        }
        else PlaceInCorner();
    }

    /// <summary>Lower-right corner of the main screen, just above the taskbar.</summary>
    private void PlaceInCorner()
    {
        var area = SystemParameters.WorkArea;
        UpdateLayout();
        Left = area.Right - ActualWidth - EdgeGap;
        Top = area.Bottom - ActualHeight - EdgeGap;
    }

    private bool IsOnScreen(double left, double top)
    {
        // The virtual screen covers every connected monitor.
        var w = Math.Max(ActualWidth, 100);
        return left >= SystemParameters.VirtualScreenLeft - w / 2
            && top >= SystemParameters.VirtualScreenTop
            && left + w / 2 <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
            && top + 40 <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
    }

    private void SavePosition()
    {
        var data = App.Current.Data;
        data.WidgetLeft = Left;
        data.WidgetTop = Top;
        App.Current.SaveData();
    }

    // ---- interaction ---------------------------------------------------------------

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { App.Current.ShowMain(); return; }
        DragMove();       // returns when the mouse button is released
        SavePosition();
    }

    // The card brightens slightly on hover so it's clear it can be dragged.
    private void Card_MouseEnter(object sender, MouseEventArgs e) => Card.Opacity = 1;
    private void Card_MouseLeave(object sender, MouseEventArgs e) => Card.Opacity = 0.92;

    private void Open_Click(object sender, RoutedEventArgs e) => App.Current.ShowMain();

    private void ResetPosition_Click(object sender, RoutedEventArgs e)
    {
        PlaceInCorner();
        SavePosition();
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => App.Current.SetWidgetVisible(false);
}
