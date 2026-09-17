namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Requests the runtime permissions the app needs, prompting the user if not already granted.
///</summary>
public interface IPermissionsService
{
    #region Public methods

    ///<summary>
    ///Ensures access to the device's audio media library is granted, prompting the user if needed.
    ///</summary>
    Task<bool> EnsureAudioLibraryPermissionAsync();

    ///<summary>
    ///Ensures microphone access is granted, prompting the user if needed. Returns whether it's granted.
    ///</summary>
    Task<bool> EnsureMicrophonePermissionAsync();
    #endregion
}
