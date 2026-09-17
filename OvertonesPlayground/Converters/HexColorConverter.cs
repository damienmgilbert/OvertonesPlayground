using System.Globalization;

namespace OvertonesPlayground.Converters;

/// <summary>Converts a "#RRGGBB" (or "#AARRGGBB") hex string into a <see cref="Color"/>; falls back to transparent for null/empty input.</summary>
public class HexColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string hex && !string.IsNullOrWhiteSpace(hex)
            ? Color.FromArgb(hex)
            : Colors.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
