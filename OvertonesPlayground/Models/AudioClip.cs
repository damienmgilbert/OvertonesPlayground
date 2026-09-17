namespace OvertonesPlayground.Models;

///<summary>
///Represents an audio clip stored in the application's library. Contains metadata such as name, file path, duration and
///import time.
///</summary>
public class AudioClip
{
    #region Public properties
    ///<summary>
    ///Playback duration of the clip.
    ///</summary>
    public TimeSpan Duration { get; set; }

    ///<summary>
    ///Local file path where the clip audio is stored.
    ///</summary>
    public string FilePath { get; set; } = string.Empty;

    ///<summary>
    ///Unique identifier for the clip.
    ///</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    ///<summary>
    ///Time when the clip was imported into the library.
    ///</summary>
    public DateTimeOffset ImportedAt { get; init; } = DateTimeOffset.Now;

    ///<summary>
    ///Indicates whether this clip was created by an in-app recording.
    ///</summary>
    public bool IsUserRecording { get; set; }

    ///<summary>
    ///User friendly name of the clip.
    ///</summary>
    public string Name { get; set; } = string.Empty;

    ///<summary>
    ///Where this clip was also copied to in shared/public storage (for example "Music/OvertonesPlayground/kick.wav").
    ///Null if it hasn't been exported.
    ///</summary>
    public string? PublicStorageLocation { get; set; }
    #endregion
}
