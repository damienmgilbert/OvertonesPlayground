namespace OvertonesPlayground.Models;

///<summary>
///Represents a single pad on the launchpad grid. Holds assignment state and playback-related settings.
///</summary>
public class LaunchpadPad
{
    #region Public properties

    ///<summary>
    ///The bank (0 to 3) the pad is in.
    ///</summary>
    public int Bank { get; set; }

    ///<summary>
    ///Path to the audio clip assigned to this pad, if any.
    ///</summary>
    public string? ClipPath { get; set; }

    ///<summary>
    ///Hex color used to display the pad when a clip is assigned.
    ///</summary>
    public string ColorHex { get; set; } = "#512BD4";

    ///<summary>
    ///True when a clip path is present and the pad has an assigned clip.
    ///</summary>
    public bool HasClip => !string.IsNullOrEmpty(ClipPath);

    ///<summary>
    ///Zero-based index of the pad within the grid.
    ///</summary>
    public int Index { get; init; }

    ///<summary>
    ///Whether the clip should loop when triggered.
    ///</summary>
    public bool IsLooping { get; set; }

    ///<summary>
    ///User-visible label for the pad.
    ///</summary>
    public string Label { get; set; } = string.Empty;

    ///<summary>
    ///Playback volume multiplier for this pad.
    ///</summary>
    public double Volume { get; set; } = 1.0;

    ///<summary>
    ///Identifies this pad's sounding voices to the playback service; different for the same position in different banks.
    ///</summary>
    public int VoiceKey => (Bank * LaunchpadProject.PadsPerBank) + Index;
    #endregion
}
