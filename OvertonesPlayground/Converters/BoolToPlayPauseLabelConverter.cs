using System.Globalization;

namespace OvertonesPlayground.Converters;

///<summary>
///Converts an "is playing" bool into the spoken name of the play/pause button's action. That button shows only an icon, so
///this is what a screen reader announces.
///</summary>
public class BoolToPlayPauseLabelConverter : IValueConverter
{
    #region Public methods
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? "Pause" : "Play";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    #endregion
}
