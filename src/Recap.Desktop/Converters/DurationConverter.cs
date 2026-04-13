using System.Globalization;
using Avalonia.Data.Converters;

namespace Recap.Desktop.Converters;

public class DurationConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
            return ts.ToString(@"mm\:ss\.f");
        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
