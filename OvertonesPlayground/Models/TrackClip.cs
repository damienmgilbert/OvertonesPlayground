namespace OvertonesPlayground.Models;

///<summary>
///One clip placed on a <see cref="Track"/>: which audio clip, where it starts, and how loud it plays within the
///track.
///</summary>
public class TrackClip
{
    #region Public properties
    ///<summary>
    ///Path to the source audio clip's WAV file.
    ///</summary>
    public string ClipFilePath { get; set; } = string.Empty;

    ///<summary>
    ///Display name of the source clip, shown in the track's clip list.
    ///</summary>
    public string ClipName { get; set; } = string.Empty;

    ///<summary>
    ///Gain applied to this clip within the track, in decibels. Zero leaves it unchanged.
    ///</summary>
    public double GainDb { get; set; }

    ///<summary>
    ///Unique identifier for this placement.
    ///</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    ///<summary>
    ///Where this clip starts playing, relative to the start of the track.
    ///</summary>
    public TimeSpan StartOffset { get; set; }
    #endregion
}
