using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CluckWise;

/// <summary>A small yes/no question in CluckWise's own style (instead of the grey Windows box).</summary>
public class CluckDialog : Window
{
    private bool _yes;

    private CluckDialog(string title, string message, string yesText, string noText)
    {
        Title = "CluckWise";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "Surface");
        SetResourceReference(ForegroundProperty, "Text");

        var logo = new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/logo.png")), Width = 56, Height = 56, HorizontalAlignment = HorizontalAlignment.Left };
        var heading = new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.ExtraBold, Margin = new Thickness(0, 12, 0, 6), TextWrapping = TextWrapping.Wrap };
        var body = new TextBlock { Text = message, FontSize = 14, TextWrapping = TextWrapping.Wrap, LineHeight = 20 };
        body.SetResourceReference(TextBlock.ForegroundProperty, "Muted");

        var yes = new Button { Content = yesText, Margin = new Thickness(10, 0, 0, 0), IsDefault = true, Style = (Style)FindResource("PrimaryButton") };
        var no = new Button { Content = noText, IsCancel = true, Style = (Style)FindResource("SecondaryButton") };
        yes.Click += (_, _) => { _yes = true; Close(); };
        no.Click += (_, _) => Close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0), Children = { no, yes } };

        Content = new StackPanel { Margin = new Thickness(28, 24, 28, 22), Children = { logo, heading, body, buttons } };
        Loaded += (_, _) => yes.Focus();
    }

    /// <summary>Shows the question and returns true if the person chose the yes button.</summary>
    public static bool Ask(string title, string message, string yesText = "Yes", string noText = "No")
    {
        var d = new CluckDialog(title, message, yesText, noText);
        d.ShowDialog();
        return d._yes;
    }
}
