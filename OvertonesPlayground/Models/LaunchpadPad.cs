namespace OvertonesPlayground.Models;

public class LaunchpadPad
{
    public int Index { get; init; }

    public string Label { get; set; } = string.Empty;

    public string? ClipPath { get; set; }

    public string ColorHex { get; set; } = "#512BD4";

    public bool IsLooping { get; set; }

    public double Volume { get; set; } = 1.0;

    public bool HasClip => !string.IsNullOrEmpty(ClipPath);
}
