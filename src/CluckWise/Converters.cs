using System.Globalization;
using System.Windows.Data;

namespace CluckWise;

/// <summary>Shows "Rooster Crow" for built-in sounds and the file name for your own sounds.</summary>
public class SoundNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string s ? Sounds.DisplayName(s) : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
