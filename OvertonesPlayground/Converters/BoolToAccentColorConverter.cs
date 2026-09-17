using System.Globalization;

namespace OvertonesPlayground.Converters;

///<summary>
///Converts a bool to the app's accent color when <c>true</c>, or transparent when <c>false</c> - used to highlight an
///active toggle (e.g. Mixer mute).
///</summary>
public class BoolToAccentColorConverter : IValueConverter
{
    #region Public methods
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) { return value is true ? Color.FromArgb("#D600AA") : Colors.Transparent; }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) { throw new NotSupportedException(); }
    #endregion
}
