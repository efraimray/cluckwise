using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace CluckWise;

/// <summary>A small celebration popup with falling confetti for someone's birthday.</summary>
public class CelebrationWindow : Window
{
    private static readonly Color[] Confetti =
    {
        Color.FromRgb(245, 165, 36), Color.FromRgb(229, 72, 77), Color.FromRgb(47, 125, 79),
        Color.FromRgb(43, 108, 176), Color.FromRgb(255, 244, 220),
    };

    public CelebrationWindow(Birthday b)
    {
        Title = "CluckWise";
        Width = 440;
        Height = 420;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        Background = (Brush)Application.Current.FindResource("Ink");

        var confetti = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
        var age = b.TurningAge(DateTime.Today);

        var thanks = new Button { Content = "Thanks, CluckWise!", Height = 50, FontSize = 16, Margin = new Thickness(0, 22, 0, 0),
                                  Style = (Style)Application.Current.FindResource("PrimaryButton") };
        thanks.Click += (_, _) => Close();

        var panel = new StackPanel { Margin = new Thickness(32, 30, 32, 28), VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/logo.png")), Width = 72, Height = 72 });
        panel.Children.Add(Text("IT'S A BIRTHDAY!", 12, FontWeights.Bold, (Brush)Application.Current.FindResource("Yolk"), new Thickness(0, 12, 0, 6)));
        panel.Children.Add(Text($"Happy birthday, {b.Name}!", 26, FontWeights.ExtraBold, Brushes.White, new Thickness(0)));
        panel.Children.Add(Text(age is { } a ? $"Turning {a} today" : "Don't forget to send your wishes",
                                15, FontWeights.Normal, (Brush)Application.Current.FindResource("SideText"), new Thickness(0, 6, 0, 0)));
        panel.Children.Add(thanks);

        Content = new Grid { Children = { confetti, panel } };

        Loaded += (_, _) =>
        {
            Sounds.Play(Sounds.SunriseChime, 70, gentle: false, loop: false);
            Drop(confetti);
            thanks.Focus();
        };
        Closed += (_, _) => Sounds.Stop();
    }

    private static TextBlock Text(string text, double size, FontWeight weight, Brush color, Thickness margin) => new()
    {
        Text = text, FontSize = size, FontWeight = weight, Foreground = color, Margin = margin,
        TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center,
    };

    private void Drop(Canvas canvas)
    {
        var rnd = new Random();
        for (int i = 0; i < 70; i++)
        {
            var piece = new Rectangle
            {
                Width = rnd.Next(6, 12), Height = rnd.Next(10, 18), RadiusX = 2, RadiusY = 2,
                Fill = new SolidColorBrush(Confetti[rnd.Next(Confetti.Length)]),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(rnd.Next(360)),
            };
            Canvas.SetLeft(piece, rnd.NextDouble() * ActualWidth);
            Canvas.SetTop(piece, -30);
            canvas.Children.Add(piece);

            var delay = TimeSpan.FromMilliseconds(rnd.Next(0, 1800));
            var fall = new DoubleAnimation(-30, ActualHeight + 30, TimeSpan.FromSeconds(2.5 + rnd.NextDouble() * 2))
            {
                BeginTime = delay,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
            };
            piece.BeginAnimation(Canvas.TopProperty, fall);
            ((RotateTransform)piece.RenderTransform).BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(0, rnd.Next(-540, 540), TimeSpan.FromSeconds(4)) { BeginTime = delay });
        }
    }
}
