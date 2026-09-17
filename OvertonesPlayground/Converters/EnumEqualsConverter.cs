using System.Globalization;

namespace OvertonesPlayground.Converters;

///<summary>
///Compares a bound enum value against <c>ConverterParameter</c> (matched by name) - handy for IsVisible toggles.
///</summary>
public class EnumEqualsConverter : IValueConverter
{
    #region Public methods
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not null && parameter is not null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    #endregion
}
