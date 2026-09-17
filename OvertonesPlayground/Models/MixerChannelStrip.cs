namespace OvertonesPlayground.Models;

/// <summary>
/// Simple model representing a mixer channel strip: id, name, pan/volume and
/// routing information.
/// </summary>
public class MixerChannelStrip
{
    /// <summary>Unique identifier for the channel.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Display name of the channel.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Linear volume multiplier (0.0 - 1.0).</summary>
    public double Volume { get; set; } = 0.8;

    /// <summary>Stereo pan value (-1.0 left to +1.0 right).</summary>
    public double Pan { get; set; }

    /// <summary>Whether the channel is muted.</summary>
    public bool IsMuted { get; set; }

    /// <summary>Whether the channel is soloed.</summary>
    public bool IsSoloed { get; set; }

    /// <summary>Path to the clip currently assigned to this channel, if any.</summary>
    public string? SourceClipPath { get; set; }
}
