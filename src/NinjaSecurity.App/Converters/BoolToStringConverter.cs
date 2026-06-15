using Avalonia.Data.Converters;
using System.Globalization;

namespace NinjaSecurity.App.Converters;

public class BoolToStringConverter : IValueConverter
{
    public static readonly BoolToStringConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var parts = parameter?.ToString()?.Split('|') ?? ["Yes", "No"];
        return value is true ? parts[0] : (parts.Length > 1 ? parts[1] : "");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
