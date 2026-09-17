namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Copies a file the app created out of app-private storage and into the device's shared Music collection, so it shows
///up in the Files app, the system Music app, and over USB/MTP - not just inside OvertonesPlayground.
///</summary>
public interface IPublicStorageService
{
    #region Public methods
    ///<summary>
    ///Exports <paramref name="sourceFilePath"/> into Music/OvertonesPlayground. Returns a short, human-readable location
    ///(e.g. "Music/OvertonesPlayground/kick.wav") on success, or null if the export could not be completed.
    ///</summary>
    Task<string?> ExportToMusicAsync(string sourceFilePath, string displayFileName);
    #endregion
}
