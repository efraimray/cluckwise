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
        Scheduler.ReminderDue += (alarm, when) =>
        {
            SaveData();
            var mins = Math.Max(1, (int)Math.Round((when - DateTime.Now).TotalMinutes));
            ShowTrayMessage($"Coming up: {alarm.Label}", $"Rings at {when:h:mm tt} (in {mins} min)");
        };
        Scheduler.TimerDone += t => { SaveData(); new RingWindow(t).Show(); };
        // Timers that finished while CluckWise was closed show as done straight away.
        foreach (var t in Data.Timers) if (t.Running) t.Tick(DateTime.Now);
        CreateTray();

        _main = new MainWindow();
        if (!e.Args.Contains("--minimized")) _main.Show();
        Scheduler.Start();

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
            ShowTrayMessage(alarm.Label, "Actions ran at " + DateTime.Now.ToString("h:mm tt"));
            return;
        }
        NativeMethods.KeepAwake(true);
        new RingWindow(alarm).Show();
    }

    private void AskAboutStartup()
    {
        Data.AskedAboutStartup = true;
        var answer = MessageBox.Show(
            "Start CluckWise when you sign in to Windows?\n\nIt opens quietly in the tray so your alarms still ring after a restart. You can change this any time in the sidebar.",
            "CluckWise", MessageBoxButton.YesNo, MessageBoxImage.Question);
        SetStartWithWindows(answer == MessageBoxResult.Yes);
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
        SaveData();
        Sounds.Stop();
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _instanceMutex?.ReleaseMutex();
        Shutdown();
    }
}
