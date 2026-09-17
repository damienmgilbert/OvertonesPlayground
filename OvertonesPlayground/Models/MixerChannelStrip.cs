namespace OvertonesPlayground.Models;

public class MixerChannelStrip
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public double Volume { get; set; } = 0.8;

    public double Pan { get; set; }

    public bool IsMuted { get; set; }

    public bool IsSoloed { get; set; }

    public string? SourceClipPath { get; set; }
}
