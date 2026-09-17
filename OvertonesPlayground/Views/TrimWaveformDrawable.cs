namespace OvertonesPlayground.Views;

///<summary>
///Renders the Trim page's waveform: a time ruler along the top, mirrored bars shaded to show what Save will keep vs.
///discard, and a playhead line at the current preview position.
///</summary>
public class TrimWaveformDrawable : IDrawable
{
    #region Constants
    private const float RulerHeight = 22f;
    #endregion

    #region Private methods
    ///<summary>
    ///Draws the tick marks and second labels for the top ruler strip.
    ///</summary>
    private void DrawRuler(ICanvas canvas, RectF dirtyRect)
    {
        bool cannotDrawTicks = DurationSeconds <= 0 || RulerStepSeconds <= 0;
        if (cannotDrawTicks)
        {
            return;
        }

        canvas.FontSize = 11;
        canvas.FontColor = Color.FromArgb("#A3A3A3");
        canvas.StrokeColor = Color.FromArgb("#3D3D3D");
        canvas.StrokeSize = 1;

        for (double seconds = 0; seconds <= DurationSeconds + 0.01; seconds += RulerStepSeconds)
        {
            float x = (float)(seconds / DurationSeconds * dirtyRect.Width);
            canvas.DrawLine(x, RulerHeight - 6, x, RulerHeight);
            canvas.DrawString($"{(int)seconds}s", x + 3, 1, 48, RulerHeight - 4, HorizontalAlignment.Left, VerticalAlignment.Top);
        }
    }

    ///<summary>
    ///Draws the mirrored bar waveform, shading bars outside Save's kept range.
    ///</summary>
    private void DrawWaveform(ICanvas canvas, RectF dirtyRect)
    {
        bool hasNothingToDraw = Peaks.Length == 0 || DurationSeconds <= 0;
        if (hasNothingToDraw)
        {
            return;
        }

        float waveTop = RulerHeight;
        float waveHeight = dirtyRect.Height - RulerHeight;
        float midY = waveTop + (waveHeight / 2);
        float stepX = dirtyRect.Width / Peaks.Length;

        float selStartX = (float)(SelectionStartSeconds / DurationSeconds * dirtyRect.Width);
        float selEndX = (float)(SelectionEndSeconds / DurationSeconds * dirtyRect.Width);

        canvas.StrokeSize = Math.Max(1f, stepX * 0.7f);

        for (int i = 0; i < Peaks.Length; i++)
        {
            float x = i * stepX;
            bool isInsideSelection = x >= selStartX && x <= selEndX;
            bool isKept = IsTrimMiddleMode ? !isInsideSelection : isInsideSelection;
            canvas.StrokeColor = isKept ? Color.FromArgb("#0078D4") : Color.FromArgb("#4D4D4D");

            float barHeight = Peaks[i] * (waveHeight / 2);
            canvas.DrawLine(x, midY - barHeight, x, midY + barHeight);
        }
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Draws the ruler, waveform, and playhead, or just the ruler if <see cref="Peaks"/> is empty.
    ///</summary>
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.Transparent;
        canvas.FillRectangle(dirtyRect);

        DrawRuler(canvas, dirtyRect);
        DrawWaveform(canvas, dirtyRect);

        bool canDrawPlayhead = DurationSeconds > 0;
        if (canDrawPlayhead)
        {
            float playX = (float)(PlayheadSeconds / DurationSeconds * dirtyRect.Width);
            canvas.StrokeColor = Color.FromArgb("#FFB900");
            canvas.StrokeSize = 2;
            canvas.DrawLine(playX, RulerHeight, playX, dirtyRect.Height);
        }
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Total duration, in seconds, the ruler and waveform positions are measured against.
    ///</summary>
    public double DurationSeconds { get; set; } = 1;

    ///<summary>
    ///Whether the [<see cref="SelectionStartSeconds"/>, <see cref="SelectionEndSeconds"/>] range is removed (true) or
    ///kept (false) by Save.
    ///</summary>
    public bool IsTrimMiddleMode { get; set; }

    ///<summary>
    ///Normalized amplitude peaks (0 to 1) to render, left to right.
    ///</summary>
    public float[] Peaks { get; set; } = [];

    ///<summary>
    ///Current preview playback position, in seconds.
    ///</summary>
    public double PlayheadSeconds { get; set; }

    ///<summary>
    ///Spacing, in seconds, between ruler tick marks.
    ///</summary>
    public double RulerStepSeconds { get; set; } = 30;

    ///<summary>
    ///End of the selected range, in seconds.
    ///</summary>
    public double SelectionEndSeconds { get; set; }

    ///<summary>
    ///Start of the selected range, in seconds.
    ///</summary>
    public double SelectionStartSeconds { get; set; }
    #endregion
}
