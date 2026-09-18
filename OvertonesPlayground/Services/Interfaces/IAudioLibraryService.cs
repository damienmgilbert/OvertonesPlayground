using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Owns the persisted catalog of audio clips: import, register, rename, and delete.
///</summary>
public interface IAudioLibraryService
{
    #region Public methods

    ///<summary>
    ///Registers an already-saved audio file (e.g. a fresh recording or an exported edit) in the library.
    ///</summary>
    Task<AudioClip> AddClipAsync(string filePath, string name, bool isUserRecording = false);

    ///<summary>
    ///Removes a clip from the library and deletes its underlying file.
    ///</summary>
    Task DeleteClipAsync(AudioClip clip);

    ///<summary>
    ///Encodes <paramref name="clip"/> to <paramref name="format"/> and exports it to the shared Music folder.
    ///Returns a short, human-readable location on success, or null if the encode succeeded but the export to shared
    ///storage did not.
    ///</summary>
    Task<string?> ExportClipAsync(AudioClip clip, AudioExportFormat format);

    ///<summary>
    ///Returns every clip currently in the library, newest first.
    ///</summary>
    Task<IReadOnlyList<AudioClip>> GetClipsAsync();

    ///<summary>
    ///Opens the system file picker, copies the chosen audio file into app storage, and adds it to the library.
    ///</summary>
    Task<AudioClip?> ImportFromPickerAsync();

    ///<summary>
    ///Renames a clip in place.
    ///</summary>
    Task RenameClipAsync(AudioClip clip, string newName);
    #endregion
}
