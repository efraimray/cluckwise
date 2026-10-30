using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace CluckWise;

public partial class RingWindow : Window
{
    /// <summary>Ringing popups that are open right now (newest last), for the global hotkeys.</summary>
    public static readonly List<RingWindow> Open = new();

    public void Snooze() => Snooze_Click(this, new RoutedEventArgs());
    public void Dismiss() => Dismiss_Click(this, new RoutedEventArgs());

    private readonly Alarm? _alarm;
    private readonly CountdownTimer? _timer;
    private bool _handled;

    public RingWindow(Alarm alarm)
    {
        InitializeComponent();
        _alarm = alarm;
        TimeText.Text = TimeFormat.Time(DateTime.Now);
        LabelText.Text = alarm.Label;
        ActionsText.Text = alarm.Actions.Count == 0 ? "" : string.Join("\n", alarm.Actions.Select(a => a.Description));
        SnoozeButton.Content = $"Snooze {alarm.SnoozeMinutes} min";
        Sounds.Play(alarm.Sound, alarm.Volume, alarm.GentleWake, loop: true);
        Loaded += (_, _) =>
        {
            DismissButton.Focus();
            if (alarm.SpeakOnRing) Voice.Say(Briefing.Greeting(DateTime.Now, alarm.Label.Replace(" (test)", "")));
        };
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
        if (_challengeShowing)
        {
            if (e.Key == System.Windows.Input.Key.Enter) { ChallengeCheck_Click(this, new RoutedEventArgs()); e.Handled = true; }
            base.OnPreviewKeyDown(e);
            return;
        }
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
        if (_alarm != null && _alarm.Challenge != ChallengeKind.None && !_challengeDone)
        {
            if (!_challengeShowing) ShowChallenge();
            return;
        }
        var data = App.Current.Data;
        // Only real alarms (not "Ring now" tests) count towards the streak.
        if (_alarm != null && data.Alarms.Contains(_alarm))
        {
            Morning.RecordDismiss(data, _alarm);
            if (Morning.IsMorningAlarm(_alarm) && data.ShowTodoAfterMorningAlarm
                && (data.Tasks.Count > 0 || Briefing.Lines(data, DateTime.Now).Count > 0))
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
        _challengeShowing = false;
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
        TaskList.Visibility = data.Tasks.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ((FrameworkElement)TaskList.Parent).Visibility = TaskList.Visibility;
        ((FrameworkElement)((FrameworkElement)TaskList.Parent).Parent).Visibility = TaskList.Visibility;
        var briefing = Briefing.Lines(data, DateTime.Now);
        BriefingList.ItemsSource = briefing;
        BriefingBox.Visibility = briefing.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_alarm is { SpeakOnRing: true } && data.BriefingAloud)
            Voice.Say((data.Streak > 1 ? $"You're on a {data.Streak} morning streak. " : "") + Briefing.Spoken(briefing));
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

    // ---- Wake-up challenge ------------------------------------------------------------

    private readonly Random _rnd = new();
    private bool _challengeShowing, _challengeDone;
    private int _solved;
    private WakeChallenge.MathsProblem? _problem;
    private string _phrase = "";
    private int[] _tiles = Array.Empty<int>();
    private int _tileStep;

    private void ShowChallenge()
    {
        _challengeShowing = true;
        _solved = 0;
        RingPanel.Visibility = Visibility.Collapsed;
        ChallengePanel.Visibility = Visibility.Visible;
        ChallengeSnooze.Content = SnoozeButton.Content + " instead";
        NextPuzzle();
    }

    private void NextPuzzle()
    {
        var a = _alarm!;
        ChallengeError.Visibility = Visibility.Collapsed;
        ChallengeInput.Clear();
        switch (a.Challenge)
        {
            case ChallengeKind.Maths:
                _problem = WakeChallenge.Maths(a.ChallengeLevel, _rnd);
                int total = WakeChallenge.MathsCount(a.ChallengeLevel);
                ChallengeTitle.Text = "Solve this to dismiss";
                ChallengeHint.Text = total > 1 ? $"Problem {_solved + 1} of {total}" : "Type the answer and press Enter";
                ChallengeQuestion.Text = _problem.Question + " = ?";
                ChallengeQuestion.FontSize = 40;
                SetMode(input: true);
                break;

            case ChallengeKind.TypePhrase:
                _phrase = WakeChallenge.Phrase(a.ChallengeLevel, _rnd);
                ChallengeTitle.Text = "Type this to dismiss";
                ChallengeHint.Text = "Capital letters don't matter";
                ChallengeQuestion.Text = "\u201c" + _phrase + "\u201d";
                ChallengeQuestion.FontSize = 20;
                SetMode(input: true);
                break;

            case ChallengeKind.TapTiles:
                _tiles = WakeChallenge.Tiles(a.ChallengeLevel, _rnd);
                _tileStep = 0;
                ChallengeTitle.Text = "Tap the numbers from smallest to largest";
                ChallengeHint.Text = "One wrong tap shuffles them";
                ChallengeQuestion.Text = "";
                SetMode(input: false);
                DrawTiles();
                break;
        }
    }

    private void SetMode(bool input)
    {
        ChallengeInput.Visibility = input ? Visibility.Visible : Visibility.Collapsed;
        ChallengeCheck.Visibility = input ? Visibility.Visible : Visibility.Collapsed;
        ChallengeQuestion.Visibility = input ? Visibility.Visible : Visibility.Collapsed;
        TileGrid.Visibility = input ? Visibility.Collapsed : Visibility.Visible;
        if (input) Dispatcher.BeginInvoke(new Action(() => ChallengeInput.Focus()), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void DrawTiles()
    {
        TileGrid.Children.Clear();
        TileGrid.Columns = WakeChallenge.TileColumns(_tiles.Length);
        var sorted = _tiles.OrderBy(n => n).ToArray();
        foreach (var n in _tiles)
        {
            bool done = Array.IndexOf(sorted, n) < _tileStep;
            var b = new Button
            {
                Content = n.ToString(), Height = 60, Margin = new Thickness(4), FontSize = 22,
                Style = (Style)FindResource(done ? "PrimaryButton" : "SecondaryButton"),
                IsEnabled = !done, Tag = n,
            };
            b.Click += Tile_Click;
            TileGrid.Children.Add(b);
        }
    }

    private void Tile_Click(object sender, RoutedEventArgs e)
    {
        int n = (int)((Button)sender).Tag;
        var sorted = _tiles.OrderBy(x => x).ToArray();
        if (n == sorted[_tileStep])
        {
            _tileStep++;
            if (_tileStep == _tiles.Length) { Solved(); return; }
        }
        else
        {
            ShowChallengeError("Not quite. They've been shuffled, try again.");
            _tiles = _tiles.OrderBy(_ => _rnd.Next()).ToArray();
            _tileStep = 0;
        }
        DrawTiles();
    }

    private void ChallengeCheck_Click(object sender, RoutedEventArgs e)
    {
        var a = _alarm!;
        var typed = ChallengeInput.Text.Trim();
        if (a.Challenge == ChallengeKind.Maths && _problem != null)
        {
            if (int.TryParse(typed, out var v) && v == _problem.Answer)
            {
                _solved++;
                if (_solved >= WakeChallenge.MathsCount(a.ChallengeLevel)) Solved(); else NextPuzzle();
            }
            else ShowChallengeError("That's not it. Try again.");
        }
        else if (a.Challenge == ChallengeKind.TypePhrase)
        {
            if (WakeChallenge.PhraseMatches(_phrase, typed)) Solved();
            else ShowChallengeError("Not an exact match yet. Check the words and try again.");
        }
    }

    private void ShowChallengeError(string text)
    {
        ChallengeError.Text = text;
        ChallengeError.Visibility = Visibility.Visible;
        ChallengeInput.SelectAll();
    }

    private void Solved()
    {
        _challengeDone = true;
        _challengeShowing = false;
        ChallengePanel.Visibility = Visibility.Collapsed;
        RingPanel.Visibility = Visibility.Visible;
        Dismiss_Click(this, new RoutedEventArgs());
    }

    private void Finish()
    {
        // Quick naps disappear once dismissed (but stay if snoozed).
        if (_alarm is { DeleteAfterRing: true, SnoozedUntil: null }) App.Current.Data.Alarms.Remove(_alarm);
        _handled = true;
        Voice.Stop();
        Sounds.Stop();
        NativeMethods.KeepAwake(false);
        App.Current.SaveData();
        Close();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Open.Add(this);
        Closed += (_, _) => Open.Remove(this);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Closing with the X counts as Dismiss so the sound never keeps playing.
        if (!_handled) { Voice.Stop(); Sounds.Stop(); NativeMethods.KeepAwake(false); _timer?.Reset(); App.Current.SaveData(); }
        base.OnClosing(e);
    }
}
