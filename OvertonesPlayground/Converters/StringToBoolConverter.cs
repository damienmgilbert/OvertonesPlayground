using System.Globalization;

namespace OvertonesPlayground.Converters;

///<summary>
///Converts a string to <c>true</c> when it's non-null and non-empty - handy for an IsVisible bound to an optional
///field.
///</summary>
public class StringToBoolConverter : IValueConverter
{
    #region Public methods
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => !string.IsNullOrEmpty(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    #endregion
}
