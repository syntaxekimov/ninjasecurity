using Avalonia.Data.Converters;
using System.Globalization;

namespace NinjaSecurity.App.Converters;

public class EqualityConverter : IValueConverter
{
    public static readonly EqualityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
