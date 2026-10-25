using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CluckWise;

public partial class MorningPage : UserControl
{
    public MorningPage()
    {
        InitializeComponent();
        var data = App.Current.Data;
        TaskList.ItemsSource = data.Tasks;
        data.Tasks.CollectionChanged += (_, _) => Refresh();
        Morning.Changed += () => Dispatcher.Invoke(Refresh);
        Refresh();
    }

    private void Refresh()
    {
        var data = App.Current.Data;
        StreakText.Text = Morning.StreakText(data);
        BestText.Text = data.BestStreak > 0 ? $"Best: {data.BestStreak}" : "Dismiss a morning alarm without snoozing";
        int done = data.Tasks.Count(t => t.Done), total = data.Tasks.Count;
        ProgressText.Text = total == 0 ? "No tasks yet" : $"{done} of {total} done";
        ProgressBar.Value = total == 0 ? 0 : (double)done / total;
        EmptyText.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Add()
    {
        var text = NewTaskBox.Text.Trim();
        if (text.Length == 0) return;
        App.Current.Data.Tasks.Add(new MorningTask { Text = text });
        NewTaskBox.Clear();
        App.Current.SaveData();
    }

    private void Add_Click(object sender, RoutedEventArgs e) => Add();

    private void NewTaskBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Add(); e.Handled = true; }
    }

    private void Task_Click(object sender, RoutedEventArgs e)
    {
        App.Current.SaveData();
        Refresh();
        Morning.NotifyChanged();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MorningTask t) App.Current.Data.Tasks.Remove(t);
        App.Current.SaveData();
    }
}
