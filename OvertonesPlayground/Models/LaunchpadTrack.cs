namespace OvertonesPlayground.Models;

///<summary>
///The sample one of the sequencer's four tracks plays. It is a copy of the pad's sample taken when the track is assigned, so
///changing or clearing the pad afterwards doesn't change the sequence.
///</summary>
public class LaunchpadTrack
{
    #region Public properties
    ///<summary>
    ///The pad column (0 to 7) the sample came from; the track plays through that column's volume, pan, speed, mute and solo.
    ///</summary>
    public int Column { get; set; }

    ///<summary>
    ///Path to the sample's audio file, or null when nothing is assigned.
    ///</summary>
    public string? ClipPath { get; set; }

    ///<summary>
    ///Hex color the track's steps are drawn in.
    ///</summary>
    public string ColorHex { get; set; } = "#FFFFFF";

    ///<summary>
    ///True when a sample is assigned.
    ///</summary>
    public bool HasSource => !string.IsNullOrEmpty(ClipPath);

    ///<summary>
    ///The sample's name.
    ///</summary>
    public string Label { get; set; } = string.Empty;
    #endregion
}
