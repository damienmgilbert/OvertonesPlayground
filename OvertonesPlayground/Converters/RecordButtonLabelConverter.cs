using System.Globalization;

namespace OvertonesPlayground.Converters;

///<summary>
///Converts the Audio Recorder's "is recording" bool into its record toggle button's label. The button's icon is set separately,
///by a trigger on the page.
///</summary>
public class RecordButtonLabelConverter : IValueConverter
{
    #region Public methods
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? "Stop Recording" : "Start Recording";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    #endregion
}
