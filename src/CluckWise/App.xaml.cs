using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;

namespace CluckWise;

public partial class App : Application
{
    private const string InstanceName = "CluckWise.SingleInstance";
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showSignal;
    private Forms.NotifyIcon? _tray;
    private MainWindow? _main;

    public static new App Current => (App)Application.Current;
    public AppData Data { get; private set; } = new();
    public Scheduler Scheduler { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Only one CluckWise at a time: a second launch just brings the first window forward.
        _instanceMutex = new Mutex(true, InstanceName, out bool first);
        if (!first)
        {
            try { EventWaitHandle.OpenExisting(InstanceName + ".Show").Set(); } catch { }
            Shutdown();
            return;
        }
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + ".Show");
        ThreadPool.RegisterWaitForSingleObject(_showSignal, (_, _) => Dispatcher.Invoke(ShowMain), null, -1, false);

        Data = Storage.Load();
        TimeFormat.Use24Hour = Data.Use24Hour;
        Theme.Apply(Data.DarkMode);
        // Every window (including popups) gets the matching title bar when it opens.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => Theme.ApplyTitleBar((Window)sender)));
        if (!Data.SeededTasks)
        {
            if (Data.Tasks.Count == 0)
                foreach (var t in new[] { "Drink a glass of water", "Stretch for 2 minutes", "Plan my top 3 tasks", "Make the bed" })
                    Data.Tasks.Add(new MorningTask { Text = t });
            Data.SeededTasks = true;
        }
        Morning.ResetIfNewDay(Data);
        Scheduler = new Scheduler(Data);
        Scheduler.AlarmDue += OnAlarmDue;
        Scheduler.VacationEnded += () => { SaveData(); ShowTrayMessage("Welcome back!", "Vacation mode is over. Your alarms are on again."); _main?.RefreshList(); };
        Scheduler.BirthdayDue += b => { SaveData(); new CelebrationWindow(b).Show(); };
        Scheduler.TaskDue += t => { SaveData(); ShowOrQueue(() => new TaskReminderWindow(t).Show()); };
        Scheduler.BirthdayTomorrow += b =>
        {
            SaveData();
            ShowTrayMessage($"Tomorrow is {b.Name}'s birthday", b.TurningAge(DateTime.Today) is { } a ? $"They're turning {a}. Time to plan something!" : "Time to plan something!");
        };
        Scheduler.ReminderDue += (alarm, when) =>
        {
            SaveData();
            var mins = Math.Max(1, (int)Math.Round((when - DateTime.Now).TotalMinutes));
            ShowOrQueue(() => ShowTrayMessage($"Coming up: {alarm.Label}", $"Rings at {TimeFormat.Time(when)} (in {mins} min)"));
        };
        Scheduler.TimerDone += t => { SaveData(); new RingWindow(t).Show(); };
        // Timers that finished while CluckWise was closed show as done straight away.
        foreach (var t in Data.Timers) if (t.Running) t.Tick(DateTime.Now);
        CreateTray();

        _main = new MainWindow();
        if (!e.Args.Contains("--minimized")) _main.Show();
        Scheduler.Start();
        if (Data.ShowWidget) SetWidgetVisible(true);
        DefineHotkeys();
        if (Data.HotkeysEnabled) GlobalHotkeys.Enable();

        try { WindowsStartup.RefreshPath(); } catch { }
        if (!Data.AskedAboutStartup) Dispatcher.BeginInvoke(new Action(AskAboutStartup));
    }

    public void SaveData()
    {
        try { Storage.Save(Data); }
        catch (Exception ex) { ShowTrayMessage("Couldn't save your alarms", ex.Message); }
    }

    private void OnAlarmDue(Alarm alarm)
    {
        SaveData();
        ActionRunner.RunAll(alarm);
        if (alarm.RunSilently)
        {
            ShowTrayMessage(alarm.Label, "Actions ran at " + TimeFormat.Time(DateTime.Now));
            return;
        }
        NativeMethods.KeepAwake(true);
        new RingWindow(alarm).Show();
    }

    private void AskAboutStartup()
    {
        Data.AskedAboutStartup = true;
        var yes = CluckDialog.Ask(
            "Start CluckWise with Windows?",
            "It opens quietly in the tray when you sign in, so your alarms still ring after a restart. You can change this any time in Settings.",
            "Yes, start with Windows", "Not now");
        SetStartWithWindows(yes);
        _main?.RefreshStartupSwitch();
    }

    public void SetStartWithWindows(bool enabled)
    {
        try
        {
            WindowsStartup.SetEnabled(enabled);
            Data.StartWithWindows = enabled;
        }
        catch (Exception ex)
        {
            ShowTrayMessage("Couldn't change the startup setting", ex.Message);
        }
        SaveData();
    }

    /// <summary>True during a Deep Work focus session: reminders wait until the break.</summary>
    public bool InFocus { get; set; }
    private readonly List<Action> _heldDuringFocus = new();

    /// <summary>Shows a reminder now, or holds it until the focus session ends.</summary>
    public void ShowOrQueue(Action show)
    {
        if (InFocus) _heldDuringFocus.Add(show);
        else show();
    }

    /// <summary>Called when a focus session ends or a break starts.</summary>
    public void ReleaseHeld()
    {
        var held = _heldDuringFocus.ToList();
        _heldDuringFocus.Clear();
        foreach (var show in held) show();
    }

    /// <summary>Raised by Ctrl+Alt+F; the Deep Work page listens to start or stop a session.</summary>
    public event Action? FocusHotkey;

    private void DefineHotkeys() => GlobalHotkeys.Define(new()
    {
        new(1, System.Windows.Input.Key.C, "Ctrl+Alt+C", "Open CluckWise", ShowMain),
        new(2, System.Windows.Input.Key.N, "Ctrl+Alt+N", "New alarm", () => { ShowMain(); _main?.NewAlarm(); }),
        new(3, System.Windows.Input.Key.S, "Ctrl+Alt+S", "Snooze the ringing alarm", () => RingWindow.Open.LastOrDefault()?.Snooze()),
        new(4, System.Windows.Input.Key.D, "Ctrl+Alt+D", "Dismiss the ringing alarm", () => RingWindow.Open.LastOrDefault()?.Dismiss()),
        new(5, System.Windows.Input.Key.W, "Ctrl+Alt+W", "Show or hide the floating widget", () => SetWidgetVisible(!WidgetVisible)),
        new(6, System.Windows.Input.Key.F, "Ctrl+Alt+F", "Start or stop a Deep Work session", () => FocusHotkey?.Invoke()),
        new(7, System.Windows.Input.Key.K, "Ctrl+Alt+K", "Add a task", () => { ShowMain(); _main?.ShowTasks(focusQuickAdd: true); }),
    });

    public void SetHotkeysEnabled(bool on)
    {
        Data.HotkeysEnabled = on;
        if (on) GlobalHotkeys.Enable(); else GlobalHotkeys.Disable();
        SaveData();
    }

    /// <summary>A one-off alarm N minutes from now that removes itself once dismissed.</summary>
    public void QuickNap(int minutes)
    {
        var at = DateTime.Now.AddMinutes(minutes);
        var nap = new Alarm
        {
            Label = minutes == 20 ? "Power nap" : minutes >= 60 ? "Nap (1 hour)" : $"Nap ({minutes} min)",
            Group = Groups.Default,
            Repeat = RepeatMode.Once,
            OnceDate = at.Date.AddYears(-1), // never rings by schedule, only via SnoozedUntil
            Hour = at.Hour,
            Minute = at.Minute,
            Enabled = false,          // rings once via SnoozedUntil, to the second
            SnoozedUntil = at,
            DeleteAfterRing = true,
            Sound = Data.DefaultSound,
            SnoozeMinutes = 5,
            GentleWake = true,
            WakeComputer = true,
        };
        Data.Alarms.Add(nap);
        SaveData();
        RefreshAlarmList();
        ShowTrayMessage(nap.Label, $"I'll wake you at {TimeFormat.Time(at)}.");
    }

    public void RefreshAlarmList() => _main?.RefreshList();
    public void TasksChanged() => _main?.UpdateTaskCount();

    private FloatingWidget? _widget;
    private Forms.ToolStripMenuItem? _widgetMenuItem;

    public bool WidgetVisible => _widget != null;

    /// <summary>Shows or hides the floating widget and remembers the choice.</summary>
    public void SetWidgetVisible(bool show)
    {
        if (show && _widget == null)
        {
            _widget = new FloatingWidget();
            _widget.Closed += (_, _) => _widget = null;
            _widget.Show();
        }
        else if (!show && _widget != null)
        {
            _widget.Close();
            _widget = null;
        }
        Data.ShowWidget = show;
        if (_widgetMenuItem != null) _widgetMenuItem.Checked = show;
        SaveData();
        _main?.RefreshWidgetSwitch();
    }

    public void ShowMain()
    {
        _main ??= new MainWindow();
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open CluckWise", null, (_, _) => ShowMain());
        menu.Items.Add("New alarm…", null, (_, _) => { ShowMain(); _main?.NewAlarm(); });
        var nap = new Forms.ToolStripMenuItem("Quick nap");
        foreach (var m in new[] { 10, 20, 30, 60 })
        {
            int minutes = m;
            nap.DropDownItems.Add(minutes == 60 ? "1 hour" : $"{minutes} minutes" + (minutes == 20 ? " (power nap)" : ""), null, (_, _) => QuickNap(minutes));
        }
        menu.Items.Add(nap);
        _widgetMenuItem = new Forms.ToolStripMenuItem("Floating widget") { CheckOnClick = false };
        _widgetMenuItem.Click += (_, _) => SetWidgetVisible(!WidgetVisible);
        menu.Items.Add(_widgetMenuItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Quit());

        _tray = new Forms.NotifyIcon
        {
            Text = "CluckWise",
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => ShowMain();
    }

    public void ShowTrayMessage(string title, string text) =>
        _tray?.ShowBalloonTip(5000, title, text, Forms.ToolTipIcon.Info);

    /// <summary>Called when the main window is closed: CluckWise keeps running in the tray.</summary>
    public void OnMainHidden()
    {
        if (Data.ShowedTrayTip) return;
        Data.ShowedTrayTip = true;
        SaveData();
        ShowTrayMessage("CluckWise is still running", "Your alarms will ring. Right-click the tray icon to quit.");
    }

    public void Quit()
    {
        GlobalHotkeys.Disable();
        SaveData();
        Sounds.Stop();
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _instanceMutex?.ReleaseMutex();
        Shutdown();
    }
}
