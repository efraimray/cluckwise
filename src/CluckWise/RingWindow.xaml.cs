using System.ComponentModel;
using System.Windows;

namespace CluckWise;

public partial class RingWindow : Window
{
    private readonly Alarm? _alarm;
    private readonly CountdownTimer? _timer;
    private bool _handled;

    public RingWindow(Alarm alarm)
    {
        InitializeComponent();
        _alarm = alarm;
        TimeText.Text = DateTime.Now.ToString("h:mm tt");
        LabelText.Text = alarm.Label;
        ActionsText.Text = alarm.Actions.Count == 0 ? "" : string.Join("\n", alarm.Actions.Select(a => a.Description));
        SnoozeButton.Content = $"Snooze {alarm.SnoozeMinutes} min";
        Sounds.Play(alarm.Sound, alarm.Volume, alarm.GentleWake, loop: true);
        Loaded += (_, _) => DismissButton.Focus();
    }

    public RingWindow(CountdownTimer timer)
    {
        InitializeComponent();
        _timer = timer;
        Title = "CluckWise timer";
        HeaderText.Text = "TIME'S UP";
        TimeText.Text = timer.DurationText;
        LabelText.Text = timer.Label;
        ActionsText.Text = "";
        SnoozeButton.Content = "Restart timer";
        Sounds.Play(timer.Sound, 80, gentle: false, loop: true);
        Loaded += (_, _) => DismissButton.Focus();
    }

    // Space or Enter = Dismiss, S = Snooze (or restart a timer), Esc = Dismiss
    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case System.Windows.Input.Key.Space when _showingTodo:
                break; // let Space tick the focused checklist item
            case System.Windows.Input.Key.Space:
            case System.Windows.Input.Key.Enter:
            case System.Windows.Input.Key.Escape:
                Dismiss_Click(this, new RoutedEventArgs()); e.Handled = true; break;
            case System.Windows.Input.Key.S:
                Snooze_Click(this, new RoutedEventArgs()); e.Handled = true; break;
        }
        base.OnPreviewKeyDown(e);
    }

    private bool _showingTodo;

    private void Dismiss_Click(object sender, RoutedEventArgs e)
    {
        if (_showingTodo) { LetsGo_Click(sender, e); return; }
        var data = App.Current.Data;
        // Only real alarms (not "Ring now" tests) count towards the streak.
        if (_alarm != null && data.Alarms.Contains(_alarm))
        {
            Morning.RecordDismiss(data, _alarm);
            if (Morning.IsMorningAlarm(_alarm) && data.ShowTodoAfterMorningAlarm && data.Tasks.Count > 0)
            {
                _alarm.SnoozedUntil = null;
                ShowTodo();
                return;
            }
        }
        if (_alarm != null) _alarm.SnoozedUntil = null;
        _timer?.Reset();
        Finish();
    }

    private void Snooze_Click(object sender, RoutedEventArgs e)
    {
        if (_showingTodo) return;
        if (_alarm != null)
        {
            _alarm.SnoozedUntil = DateTime.Now.AddMinutes(Math.Max(1, _alarm.SnoozeMinutes));
            _alarm.SnoozedThisRing = true;
        }
        if (_timer != null) { _timer.Reset(); _timer.Start(); }
        Finish();
    }

    private void ShowTodo()
    {
        _showingTodo = true;
        Sounds.Stop();
        var data = App.Current.Data;
        StreakText.Text = Morning.StreakText(data);
        StreakHint.Text = data.Streak > 0
            ? (data.BestStreak > data.Streak ? $"Your best is {data.BestStreak}. Keep going!" : "That's your best streak yet!")
            : "Dismiss without snoozing to build a streak.";
        TaskList.ItemsSource = data.Tasks;
        RingPanel.Visibility = Visibility.Collapsed;
        TodoPanel.Visibility = Visibility.Visible;
        App.Current.SaveData();
    }

    private void Task_Click(object sender, RoutedEventArgs e)
    {
        App.Current.SaveData();
        Morning.NotifyChanged();
    }

    private void LetsGo_Click(object sender, RoutedEventArgs e) => Finish();

    private void Finish()
    {
        _handled = true;
        Sounds.Stop();
        App.Current.SaveData();
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Closing with the X counts as Dismiss so the sound never keeps playing.
        if (!_handled) { Sounds.Stop(); _timer?.Reset(); App.Current.SaveData(); }
        base.OnClosing(e);
    }
}
