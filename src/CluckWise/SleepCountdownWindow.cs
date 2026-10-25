using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace CluckWise;

/// <summary>"Going to sleep in 30 s" with a Cancel button, so an alarm action never surprises you.</summary>
public class SleepCountdownWindow : Window
{
    private int _seconds = 30;
    private readonly TextBlock _text = new() { FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 16) };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public SleepCountdownWindow()
    {
        Title = "CluckWise";
        Width = 380;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        Background = (Brush)Application.Current.FindResource("Ink");

        var cancel = new Button { Content = "Cancel", Height = 48, Style = (Style)Application.Current.FindResource("PrimaryButton") };
        cancel.Click += (_, _) => Close();
        Content = new StackPanel { Margin = new Thickness(28), Children = { _text, cancel } };

        _timer.Tick += (_, _) => Tick();
        Loaded += (_, _) => { Render(); _timer.Start(); cancel.Focus(); };
        Closed += (_, _) => _timer.Stop();
    }

    private void Render() => _text.Text = $"Putting the PC to sleep in {_seconds} s";

    private void Tick()
    {
        if (--_seconds > 0) { Render(); return; }
        _timer.Stop();
        Close();
        App.Current.SaveData();
        System.Windows.Forms.Application.SetSuspendState(System.Windows.Forms.PowerState.Suspend, false, false);
    }
}
