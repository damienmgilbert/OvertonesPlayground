namespace OvertonesPlayground.Views;

///<summary>
///Renders a simple mirrored bar-chart waveform from a set of amplitude peaks.
///</summary>
public class WaveformDrawable : IDrawable
{
    #region Fields
    // Parsed once: Draw is a hot path, so it must not parse a hex string (and allocate a Color) on every redraw.
    private static readonly Color BarColor = Color.FromArgb("#512BD4");
    #endregion

    #region Public methods

    ///<summary>
    ///Draws the waveform, or nothing if <see cref="Peaks"/> is empty.
    ///</summary>
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.Transparent;
        canvas.FillRectangle(dirtyRect);

        if (Peaks.Length == 0)
        {
            return;
        }

        canvas.StrokeColor = BarColor;
        canvas.StrokeSize = Math.Max(1f, dirtyRect.Width / Peaks.Length * 0.7f);

        float midY = dirtyRect.Height / 2;
        float stepX = dirtyRect.Width / Peaks.Length;

        for (int i = 0; i < Peaks.Length; i++)
        {
            float x = i * stepX;
            float barHeight = Peaks[i] * midY;
            canvas.DrawLine(x, midY - barHeight, x, midY + barHeight);
        }
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Normalized amplitude peaks (0 to 1) to render, left to right.
    ///</summary>
    public float[] Peaks { get; set; } = [];
    #endregion
}
