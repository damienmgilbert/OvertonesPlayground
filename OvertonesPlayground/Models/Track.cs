namespace OvertonesPlayground.Models;

///<summary>
///One track in a <see cref="MixProject"/>: an ordered set of clip placements plus the track's own volume/pan/mute/solo,
///mirroring the controls <c>MixerChannelViewModel</c> already exposes for live playback.
///</summary>
public class Track
{
    #region Public properties
    ///<summary>
    ///Every clip placed on this track.
    ///</summary>
    public List<TrackClip> Clips { get; init; } = [];

    ///<summary>
    ///Unique identifier for this track.
    ///</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    ///<summary>
    ///Whether this track is excluded from mixdown.
    ///</summary>
    public bool IsMuted { get; set; }

    ///<summary>
    ///Whether this track is soloed. If any track in the project is soloed, only soloed tracks are included in the
    ///mixdown.
    ///</summary>
    public bool IsSoloed { get; set; }

    ///<summary>
    ///Display name for the track.
    ///</summary>
    public string Name { get; set; } = string.Empty;

    ///<summary>
    ///Stereo pan for this track's output, from -1 (left) to 1 (right).
    ///</summary>
    public double Pan { get; set; }

    ///<summary>
    ///Track volume (linear), from 0 (silent) to 1 (full scale).
    ///</summary>
    public double Volume { get; set; } = 1.0;
    #endregion
}
