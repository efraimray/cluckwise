using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CluckWise;

public partial class AlarmEditorPanel : UserControl
{
    private Alarm _edit = new();
    private Alarm _original = new();
    private bool _initialized;
    private readonly ToggleButton[] _days = new ToggleButton[7];
    private List<AlarmAction> _actions = new();
    private bool _loading = true;
    private bool _previewing;


    public AlarmEditorPanel() => InitializeComponent();

    private MainWindow Main => (MainWindow)Window.GetWindow(this);

    private void Panel_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized || DataContext is not Alarm alarm) return;
        _initialized = true;
        Init(alarm, alarm.IsNew, App.Current.Data.Alarms.Where(a => !a.IsNew).Select(a => a.Group).Distinct());
    }

    private void Init(Alarm alarm, bool isNew, IEnumerable<string> groups)
    {
        _original = alarm;
        _edit = alarm.Clone();
        _actions = _edit.Actions.ToList();
        DeleteButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;

        for (int h = 1; h <= 12; h++) HourBox.Items.Add(h.ToString());
        for (int m = 0; m < 60; m++) MinuteBox.Items.Add(m.ToString("00"));
        string[] letters = { "S", "M", "T", "W", "T", "F", "S" };
        for (int i = 0; i < 7; i++)
        {
            _days[i] = new ToggleButton { Content = letters[i], Style = (Style)FindResource("DayChip"), Margin = new Thickness(0, 0, 6, 0) };
            _days[i].Click += (_, _) => UpdateState();
            DaysPanel.Children.Add(_days[i]);
        }
        foreach (var g in groups.Concat(new[] { "Morning", "Work", "Exercise" }).Distinct()) GroupBox.Items.Add(g);
        foreach (var s in Sounds.BuiltIn) SoundBox.Items.Add(s);
        if (!Sounds.BuiltIn.Contains(_edit.Sound)) SoundBox.Items.Add(_edit.Sound);
        foreach (var n in new[] { 1, 3, 5, 9, 10, 15, 20, 30 }) SnoozeBox.Items.Add($"{n} min");

        HourBox.SelectedIndex = (_edit.Hour % 12 == 0 ? 12 : _edit.Hour % 12) - 1;
        MinuteBox.SelectedIndex = _edit.Minute;
        AmPmBox.SelectedIndex = _edit.Hour < 12 ? 0 : 1;
        OnceRadio.IsChecked = _edit.Repeat == RepeatMode.Once;
        DailyRadio.IsChecked = _edit.Repeat == RepeatMode.Daily;
        WeeklyRadio.IsChecked = _edit.Repeat == RepeatMode.Weekly;
        for (int i = 0; i < 7; i++) _days[i].IsChecked = _edit.Days[i];
        OnceDatePicker.SelectedDate = _edit.OnceDate < DateTime.Today ? DateTime.Today : _edit.OnceDate;
        LabelBox.Text = _edit.Label;
        GroupBox.Text = isNew ? "Morning" : _edit.Group;
        SoundBox.SelectedItem = _edit.Sound;
        VolumeSlider.Value = _edit.Volume;
        GentleBox.IsChecked = _edit.GentleWake;
        SnoozeBox.SelectedItem = $"{_edit.SnoozeMinutes} min";
        if (SnoozeBox.SelectedIndex < 0) SnoozeBox.SelectedItem = "9 min";
        SilentBox.IsChecked = _edit.RunSilently;
        EnabledBox.IsChecked = isNew || _edit.Enabled;
        RefreshActions();

        _loading = false;
        UpdateState();
        LabelBox.Focus();
        Unloaded += (_, _) => { if (_previewing) Sounds.Stop(); };
    }

    private void Changed(object sender, RoutedEventArgs e) => UpdateState();

    /// <summary>Copies the form into the working copy and refreshes the summary line.</summary>
    private void UpdateState()
    {
        if (_loading) return;
        int hour12 = HourBox.SelectedIndex + 1;
        _edit.Hour = hour12 % 12 + (AmPmBox.SelectedIndex == 1 ? 12 : 0);
        _edit.Minute = Math.Max(0, MinuteBox.SelectedIndex);
        _edit.Repeat = OnceRadio.IsChecked == true ? RepeatMode.Once
                     : DailyRadio.IsChecked == true ? RepeatMode.Daily : RepeatMode.Weekly;
        _edit.Days = _days.Select(d => d.IsChecked == true).ToArray();
        _edit.OnceDate = OnceDatePicker.SelectedDate ?? DateTime.Today;

        DaysPanel.Visibility = _edit.Repeat == RepeatMode.Weekly ? Visibility.Visible : Visibility.Collapsed;
        OnceDatePicker.Visibility = _edit.Repeat == RepeatMode.Once ? Visibility.Visible : Visibility.Collapsed;

        var next = Scheduler.NextOccurrence(_edit, DateTime.Now);
        SummaryText.Text = next is { } n
            ? $"Rings {n:dddd d MMM} at {n:h:mm tt} · in {Scheduler.Humanize(n - DateTime.Now)}"
            : _edit.Repeat == RepeatMode.Weekly ? "Pick at least one day" : "That time has already passed";
    }

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        VolumeText.Text = $"{(int)VolumeSlider.Value}%";

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (_previewing) { Sounds.Stop(); _previewing = false; PreviewButton.Content = "Play"; return; }
        if (SoundBox.SelectedItem is not string sound) return;
        Sounds.Play(sound, (int)VolumeSlider.Value, gentle: false, loop: false);
        _previewing = true;
        PreviewButton.Content = "Stop";
    }

    private void BrowseSound_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Sound files|*.mp3;*.wav;*.wma;*.m4a|All files|*.*" };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        if (!SoundBox.Items.Contains(dlg.FileName)) SoundBox.Items.Add(dlg.FileName);
        SoundBox.SelectedItem = dlg.FileName;
    }

    private void AddWebsite_Click(object sender, RoutedEventArgs e)
    {
        var text = Microsoft.VisualBasic.Interaction.InputBox("Web address to open when the alarm rings:", "Open a website", "https://");
        if (string.IsNullOrWhiteSpace(text) || text.Trim() == "https://") return;
        var url = ActionRunner.NormalizeUrl(text);
        if (url == null) { MessageBox.Show(Window.GetWindow(this), "That doesn't look like a web address.", "CluckWise"); return; }
        _actions.Add(new AlarmAction { Kind = ActionKind.OpenWebsite, Target = url });
        RefreshActions();
    }

    private void AddProgram_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Choose an app or file to open", Filter = "Apps and files|*.*" };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        _actions.Add(new AlarmAction { Kind = ActionKind.OpenProgram, Target = dlg.FileName });
        RefreshActions();
    }

    private void TestAction_Click(object sender, RoutedEventArgs e)
    {
        if (ActionsList.SelectedItem is not AlarmAction a) { MessageBox.Show(Window.GetWindow(this), "Select an action to test.", "CluckWise"); return; }
        var error = ActionRunner.Run(a);
        if (error != null) MessageBox.Show(Window.GetWindow(this), error, "CluckWise");
    }

    private void RemoveAction_Click(object sender, RoutedEventArgs e)
    {
        if (ActionsList.SelectedItem is AlarmAction a) { _actions.Remove(a); RefreshActions(); }
    }

    private void RefreshActions()
    {
        ActionsList.ItemsSource = null;
        ActionsList.ItemsSource = _actions;
        NoActionsText.Visibility = _actions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        UpdateState();
        if (_edit.Repeat == RepeatMode.Weekly && !_edit.Days.Any(d => d))
        {
            MessageBox.Show(Window.GetWindow(this), "Pick at least one day, or choose Once or Every day.", "CluckWise");
            return;
        }
        _edit.Label = string.IsNullOrWhiteSpace(LabelBox.Text) ? "Alarm" : LabelBox.Text.Trim();
        _edit.Group = string.IsNullOrWhiteSpace(GroupBox.Text) ? "Other" : GroupBox.Text.Trim();
        _edit.Sound = SoundBox.SelectedItem as string ?? Sounds.RoosterCrow;
        _edit.Volume = (int)VolumeSlider.Value;
        _edit.GentleWake = GentleBox.IsChecked == true;
        _edit.SnoozeMinutes = int.Parse(((string)SnoozeBox.SelectedItem).Split(' ')[0]);
        _edit.RunSilently = SilentBox.IsChecked == true;
        _edit.Enabled = EnabledBox.IsChecked == true;
        _edit.Actions = _actions.ToList();
        Main.EditorSaved(_original, _edit);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        Main.EditorDeleted(_original);
    }

    private void RingNow_Click(object sender, RoutedEventArgs e)
    {
        StopPreviewIfPlaying();
        UpdateState();
        var test = _edit.Clone();
        test.Label = (string.IsNullOrWhiteSpace(LabelBox.Text) ? "Alarm" : LabelBox.Text.Trim()) + " (test)";
        test.Sound = SoundBox.SelectedItem as string ?? Sounds.RoosterCrow;
        test.Volume = (int)VolumeSlider.Value;
        test.GentleWake = GentleBox.IsChecked == true;
        test.Actions = new();
        new RingWindow(test) { Owner = Window.GetWindow(this) }.Show();
    }

    private void StopPreviewIfPlaying()
    {
        if (!_previewing) return;
        Sounds.Stop();
        _previewing = false;
        PreviewButton.Content = "Play";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Main.EditorCancelled(_original);
}
