namespace OvertonesPlayground.Views;

///<summary>
///Renders the Trim page's waveform: a time ruler along the top, mirrored bars shaded to show what Save will keep vs.
///discard, and a playhead line at the current preview position. Every position is relative to the current zoom
///window (<see cref="WindowStartSeconds"/>/<see cref="VisibleSeconds"/>), not the clip's full duration.
///</summary>
public class TrimWaveformDrawable : IDrawable
{
    #region Constants
    private const float RulerHeight = 22f;
    #endregion

    #region Private methods
    ///<summary>
    ///Draws the tick marks and second labels for the top ruler strip, for ticks that fall within the visible window.
    ///</summary>
    private void DrawRuler(ICanvas canvas, RectF dirtyRect)
    {
        bool cannotDrawTicks = VisibleSeconds <= 0 || RulerStepSeconds <= 0;
        if (cannotDrawTicks)
        {
            return;
        }

        canvas.FontSize = 11;
        canvas.FontColor = Color.FromArgb("#A3A3A3");
        canvas.StrokeColor = Color.FromArgb("#3D3D3D");
        canvas.StrokeSize = 1;

        double windowEnd = WindowStartSeconds + VisibleSeconds;
        double firstTick = Math.Ceiling(WindowStartSeconds / RulerStepSeconds) * RulerStepSeconds;

        for (double seconds = firstTick; seconds <= windowEnd + 0.01; seconds += RulerStepSeconds)
        {
            float x = TimeToX(seconds, dirtyRect);
            canvas.DrawLine(x, RulerHeight - 6, x, RulerHeight);
            canvas.DrawString($"{(int)Math.Round(seconds)}s", x + 3, 1, 48, RulerHeight - 4, HorizontalAlignment.Left, VerticalAlignment.Top);
        }
    }

    ///<summary>
    ///Draws the mirrored bar waveform for the peaks that fall within the visible window, shading bars outside Save's
    ///kept range.
    ///</summary>
    private void DrawWaveform(ICanvas canvas, RectF dirtyRect)
    {
        bool hasNothingToDraw = Peaks.Length == 0 || DurationSeconds <= 0 || VisibleSeconds <= 0;
        if (hasNothingToDraw)
        {
            return;
        }

        double peaksPerSecond = Peaks.Length / DurationSeconds;
        int firstPeak = Math.Clamp((int)(WindowStartSeconds * peaksPerSecond), 0, Peaks.Length - 1);
        int lastPeak = Math.Clamp((int)Math.Ceiling((WindowStartSeconds + VisibleSeconds) * peaksPerSecond), firstPeak + 1, Peaks.Length);

        float waveTop = RulerHeight;
        float waveHeight = dirtyRect.Height - RulerHeight;
        float midY = waveTop + (waveHeight / 2);
        float stepX = dirtyRect.Width / (lastPeak - firstPeak);

        float selStartX = TimeToX(SelectionStartSeconds, dirtyRect);
        float selEndX = TimeToX(SelectionEndSeconds, dirtyRect);

        canvas.StrokeSize = Math.Max(1f, stepX * 0.7f);

        for (int i = firstPeak; i < lastPeak; i++)
        {
            float x = (i - firstPeak) * stepX;
            bool isInsideSelection = x >= selStartX && x <= selEndX;
            bool isKept = IsTrimMiddleMode ? !isInsideSelection : isInsideSelection;
            canvas.StrokeColor = isKept ? Color.FromArgb("#0078D4") : Color.FromArgb("#4D4D4D");

            float barHeight = Peaks[i] * (waveHeight / 2);
            canvas.DrawLine(x, midY - barHeight, x, midY + barHeight);
        }
    }

    ///<summary>
    ///Converts a clip-relative time into an x coordinate within the current zoom window.
    ///</summary>
    private float TimeToX(double seconds, RectF dirtyRect) => (float)((seconds - WindowStartSeconds) / VisibleSeconds * dirtyRect.Width);
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

        bool canDrawPlayhead = VisibleSeconds > 0;
        if (canDrawPlayhead)
        {
            float playX = TimeToX(PlayheadSeconds, dirtyRect);
            bool isPlayheadVisible = playX >= 0 && playX <= dirtyRect.Width;
            if (isPlayheadVisible)
            {
                canvas.StrokeColor = Color.FromArgb("#FFB900");
                canvas.StrokeSize = 2;
                canvas.DrawLine(playX, RulerHeight, playX, dirtyRect.Height);
            }
        }
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Total duration, in seconds, of the loaded clip - used only to map <see cref="Peaks"/> indices to time.
    ///</summary>
    public double DurationSeconds { get; set; } = 1;

    ///<summary>
    ///Whether the [<see cref="SelectionStartSeconds"/>, <see cref="SelectionEndSeconds"/>] range is removed (true) or
    ///kept (false) by Save.
    ///</summary>
    public bool IsTrimMiddleMode { get; set; }

    ///<summary>
    ///Normalized amplitude peaks (0 to 1) for the whole clip, left to right.
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

    ///<summary>
    ///How many seconds of the clip are currently visible across the drawing's full width.
    ///</summary>
    public double VisibleSeconds { get; set; } = 1;

    ///<summary>
    ///Clip-relative time, in seconds, at the left edge of the drawing.
    ///</summary>
    public double WindowStartSeconds { get; set; }
    #endregion
}
