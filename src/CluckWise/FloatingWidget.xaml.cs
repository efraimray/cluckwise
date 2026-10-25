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
            NextText.Text = $"{TimeFormat.Time(n.when)} · {n.alarm.Label}";
            NextWhen.Text = "in " + Scheduler.Humanize(n.when - now);
        }
        else
        {
            NextText.Text = "No alarms on";
            NextWhen.Text = "Double-click to add one";
        }

        var timer = data.Timers.Where(t => t.Running).OrderBy(t => t.RemainingSeconds).FirstOrDefault();
        TimerPanel.Visibility = timer == null ? Visibility.Collapsed : Visibility.Visible;
        if (timer != null)
        {
            int others = data.Timers.Count(t => t.Running) - 1;
            TimerText.Text = $"{timer.Label} · {timer.RemainingText}" + (others > 0 ? $"  (+{others} more)" : "");
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

    private void Card_MouseEnter(object sender, MouseEventArgs e) => CloseButton.Opacity = 1;
    private void Card_MouseLeave(object sender, MouseEventArgs e) => CloseButton.Opacity = 0;

    private void Open_Click(object sender, RoutedEventArgs e) => App.Current.ShowMain();

    private void ResetPosition_Click(object sender, RoutedEventArgs e)
    {
        PlaceInCorner();
        SavePosition();
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => App.Current.SetWidgetVisible(false);
}
