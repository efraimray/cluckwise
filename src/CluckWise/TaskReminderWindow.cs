using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CluckWise;

/// <summary>"Reminder: Call Mum" with Done / Snooze 10 min / Tomorrow.</summary>
public class TaskReminderWindow : Window
{
    private readonly TaskItem _task;

    public TaskReminderWindow(TaskItem task)
    {
        _task = task;
        Title = "CluckWise reminder";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        Background = (Brush)Application.Current.FindResource("Ink");

        TextBlock T(string text, double size, FontWeight w, string brushKey, Thickness m) =>
            new() { Text = text, FontSize = size, FontWeight = w, Foreground = (Brush)Application.Current.FindResource(brushKey), Margin = m, TextWrapping = TextWrapping.Wrap };

        var panel = new StackPanel { Margin = new Thickness(28, 24, 28, 24) };
        panel.Children.Add(T("REMINDER", 12, FontWeights.Bold, "Yolk", new Thickness(0)));
        var title = T(task.Title, 22, FontWeights.ExtraBold, "Ink", new Thickness(0, 6, 0, 4));
        title.Foreground = Brushes.White;
        panel.Children.Add(title);
        var when = task.Due is { } d ? TaskItem.Friendly(d, task.HasTime) : "";
        if (task.Priority == TaskPriority.High) when += "  ·  High priority";
        panel.Children.Add(T(when, 14, FontWeights.Normal, "SideText", new Thickness(0, 0, 0, 4)));
        if (!string.IsNullOrWhiteSpace(task.Notes))
            panel.Children.Add(T(task.Notes, 13, FontWeights.Normal, "SideText", new Thickness(0, 4, 0, 0)));

        var done = Btn("Done", "PrimaryButton");
        var snooze = Btn("Snooze 10 min", "SecondaryButton");
        var tomorrow = Btn("Tomorrow", "SecondaryButton");
        done.Click += (_, _) => { _task.Done = true; Finish(); };
        snooze.Click += (_, _) => { _task.SnoozedUntil = DateTime.Now.AddMinutes(10); _task.Reminded = false; Finish(); };
        tomorrow.Click += (_, _) =>
        {
            _task.Due = (_task.Due ?? DateTime.Now).AddDays(1);
            _task.SnoozedUntil = null;
            _task.Reminded = false;
            _task.RefreshAll();
            Finish();
        };
        var row = new UniformGrid { Columns = 3, Margin = new Thickness(0, 20, 0, 0), Children = { done, snooze, tomorrow } };
        panel.Children.Add(row);
        Content = panel;

        Loaded += (_, _) => { Sounds.Play(Sounds.GentleBeep, 60, gentle: false, loop: false); done.Focus(); };
        Closed += (_, _) => Sounds.Stop();
    }

    private static Button Btn(string text, string style) =>
        new() { Content = text, Height = 44, Margin = new Thickness(3, 0, 3, 0), Padding = new Thickness(8, 0, 8, 0), Style = (Style)Application.Current.FindResource(style) };

    private void Finish()
    {
        App.Current.SaveData();
        Close();
    }
}
