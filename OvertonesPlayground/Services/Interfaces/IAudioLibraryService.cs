using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Interfaces;

public interface IAudioLibraryService
{
    Task<IReadOnlyList<AudioClip>> GetClipsAsync();

    /// <summary>Opens the system file picker, copies the chosen audio file into app storage, and adds it to the library.</summary>
    Task<AudioClip?> ImportFromPickerAsync();

    /// <summary>Registers an already-saved audio file (e.g. a fresh recording or an exported edit) in the library.</summary>
    Task<AudioClip> AddClipAsync(string filePath, string name, bool isUserRecording = false);

    Task DeleteClipAsync(AudioClip clip);

    Task RenameClipAsync(AudioClip clip, string newName);
}
