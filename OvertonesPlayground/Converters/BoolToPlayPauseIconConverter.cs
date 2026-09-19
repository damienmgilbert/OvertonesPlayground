using System.Globalization;
using OvertonesPlayground.Models;

namespace OvertonesPlayground.Converters;

///<summary>
///Converts an "is playing" bool into a play/pause icon glyph for <c>controls:Icon.Glyph</c>: pause when <c>true</c>, play when
///<c>false</c>.
///</summary>
public class BoolToPlayPauseIconConverter : IValueConverter
{
    #region Public methods
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? IconFont.Pause : IconFont.Play_arrow;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    #endregion
}
