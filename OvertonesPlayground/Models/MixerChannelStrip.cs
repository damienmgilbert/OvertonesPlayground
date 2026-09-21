namespace OvertonesPlayground.Models;

///<summary>
///Simple model representing a mixer channel strip: id, name, pan/volume and routing information.
///</summary>
public class MixerChannelStrip
{
    #region Public properties
    ///<summary>
    ///Hex color assigned to the channel strip for at-a-glance identification, matching the round-robin palette used by
    ///the Launchpad.
    ///</summary>
    public string ColorHex { get; set; } = "#512BD4";

    ///<summary>
    ///Unique identifier for the channel.
    ///</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    ///<summary>
    ///Name of the clip assigned to this channel, kept so a saved setup can show it again.
    ///</summary>
    public string? SourceName { get; set; }

    ///<summary>
    ///Whether the channel repeats its sample until stopped (the default) or plays it once.
    ///</summary>
    public bool IsLooping { get; set; } = true;

    ///<summary>
    ///Whether the channel is muted.
    ///</summary>
    public bool IsMuted { get; set; }

    ///<summary>
    ///Whether the channel is soloed.
    ///</summary>
    public bool IsSoloed { get; set; }

    ///<summary>
    ///Whether another channel's solo is silencing this one. Set by the mixer, which is the only place that knows about the
    ///other channels.
    ///</summary>
    public bool IsSilencedBySolo { get; set; }

    ///<summary>
    ///The volume this channel should actually sound at: silent while muted or silenced by another channel's solo.
    ///</summary>
    public double AudibleVolume => IsMuted || IsSilencedBySolo ? 0 : Volume;

    ///<summary>
    ///Display name of the channel.
    ///</summary>
    public string Name { get; set; } = string.Empty;

    ///<summary>
    ///Stereo pan value (-1.0 left to +1.0 right).
    ///</summary>
    public double Pan { get; set; }

    ///<summary>
    ///Path to the clip currently assigned to this channel, if any.
    ///</summary>
    public string? SourceClipPath { get; set; }

    ///<summary>
    ///Linear volume multiplier (0.0 - 1.0).
    ///</summary>
    public double Volume { get; set; } = 0.8;
    #endregion
}
