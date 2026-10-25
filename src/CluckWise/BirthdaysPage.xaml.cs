using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CluckWise;

public partial class BirthdaysPage : UserControl
{
    private readonly ICollectionView _view;

    public BirthdaysPage()
    {
        InitializeComponent();
        var list = App.Current.Data.Birthdays;
        BirthdayList.ItemsSource = list;
        _view = CollectionViewSource.GetDefaultView(list);
        list.CollectionChanged += (_, _) => Refresh();
        DateBox.SelectedDate = DateTime.Today;
        IsVisibleChanged += (_, _) => { if (IsVisible) Refresh(); };
        Refresh();
    }

    /// <summary>Updates countdowns and sorts the soonest birthday to the top.</summary>
    public void Refresh()
    {
        foreach (var b in App.Current.Data.Birthdays) b.Refresh(DateTime.Today);
        using (_view.DeferRefresh())
        {
            _view.SortDescriptions.Clear();
            _view.SortDescriptions.Add(new SortDescription(nameof(Birthday.DaysUntil), ListSortDirection.Ascending));
        }
        EmptyText.Visibility = App.Current.Data.Birthdays.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0) { ShowError("Type a name first."); return; }
        if (DateBox.SelectedDate is not { } date) { ShowError("Pick their birthday."); return; }
        if (KnowYearBox.IsChecked == true && date.Date > DateTime.Today) { ShowError("A birth year can't be in the future."); return; }
        ErrorText.Visibility = Visibility.Collapsed;

        var b = new Birthday
        {
            Name = name, Month = date.Month, Day = date.Day,
            Year = KnowYearBox.IsChecked == true ? date.Year : null,
            // Don't pop a celebration for a birthday added on the day itself.
            LastCelebratedYear = date.Month == DateTime.Today.Month && date.Day == DateTime.Today.Day ? DateTime.Today.Year : 0,
        };
        App.Current.Data.Birthdays.Add(b);
        App.Current.SaveData();
        NameBox.Clear();
        KnowYearBox.IsChecked = false;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is Birthday b) App.Current.Data.Birthdays.Remove(b);
        App.Current.SaveData();
    }
}
