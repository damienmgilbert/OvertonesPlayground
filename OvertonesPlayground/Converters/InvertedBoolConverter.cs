using System.Globalization;

namespace OvertonesPlayground.Converters;

///<summary>
///Logically negates a bool value; symmetric, so it also works for two-way bindings.
///</summary>
public class InvertedBoolConverter : IValueConverter
{
    #region Public methods
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) { return value is bool b && !b; }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) { return value is bool b && !b; }
    #endregion
}
