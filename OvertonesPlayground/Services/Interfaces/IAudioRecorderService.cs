using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Captures microphone input to a WAV file for the Audio Recorder page.
///</summary>
public interface IAudioRecorderService
{
    #region Events
    ///<summary>
    ///Raised periodically while recording, reporting the updated <see cref="Elapsed"/> time.
    ///</summary>
    event EventHandler<TimeSpan>? ElapsedChanged;
    #endregion

    #region Public methods
    ///<summary>
    ///Stops recording and discards the captured file.
    ///</summary>
    Task CancelAsync();

    ///<summary>
    ///Starts recording to a new file in app-private storage.
    ///</summary>
    Task StartAsync();

    ///<summary>
    ///Stops recording and returns a clip pointing at the captured file (not yet added to the library).
    ///</summary>
    Task<AudioClip> StopAsync(string name);
    #endregion

    #region Public properties
    ///<summary>
    ///How long the current (or most recent) recording has run.
    ///</summary>
    TimeSpan Elapsed { get; }

    ///<summary>
    ///Whether a recording is currently in progress.
    ///</summary>
    bool IsRecording { get; }
    #endregion
}
