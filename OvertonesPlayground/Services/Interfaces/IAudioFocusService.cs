namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Wraps the platform's audio focus API so playback ducks/pauses correctly when a phone call, notification sound, or
///another music app wants the speaker.
///</summary>
public interface IAudioFocusService
{
    #region Events

    ///<summary>
    ///Raised when focus is gained or lost; <c>true</c> means the app currently holds focus.
    ///</summary>
    event EventHandler<bool>? FocusChanged;
    #endregion

    #region Public methods
    ///<summary>
    ///Releases audio focus, allowing other apps to take it.
    ///</summary>
    void AbandonFocus();

    ///<summary>
    ///Requests exclusive audio focus. Returns whether it was granted.
    ///</summary>
    bool RequestFocus();
    #endregion
}
