using System.Globalization;

namespace OvertonesPlayground.Converters;

/// <summary>Converts an "is playing" bool into a play/pause glyph: ⏸ when <c>true</c>, ▶ when <c>false</c>.</summary>
public class BoolToPlayPauseIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "⏸" : "▶";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
