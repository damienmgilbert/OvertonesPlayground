namespace OvertonesPlayground.Views;

/// <summary>Renders a simple mirrored bar-chart waveform from a set of amplitude peaks.</summary>
public class WaveformDrawable : IDrawable
{
    /// <summary>Normalized amplitude peaks (0 to 1) to render, left to right.</summary>
    public float[] Peaks { get; set; } = [];

    /// <summary>Draws the waveform, or nothing if <see cref="Peaks"/> is empty.</summary>
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.Transparent;
        canvas.FillRectangle(dirtyRect);

        if (Peaks.Length == 0)
        {
            return;
        }

        canvas.StrokeColor = Color.FromArgb("#512BD4");
        canvas.StrokeSize = Math.Max(1f, dirtyRect.Width / Peaks.Length * 0.7f);

        var midY = dirtyRect.Height / 2;
        var stepX = dirtyRect.Width / Peaks.Length;

        for (var i = 0; i < Peaks.Length; i++)
        {
            var x = i * stepX;
            var barHeight = Peaks[i] * midY;
            canvas.DrawLine(x, midY - barHeight, x, midY + barHeight);
        }
    }
}
