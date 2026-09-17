using System.Text.Json;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;
using Plugin.Maui.Audio;

namespace OvertonesPlayground.Services.Implementations;

///<inheritdoc cref="IAudioLibraryService"/>
public class AudioLibraryService : IAudioLibraryService
{
    #region Constants
    private const string LibraryFileName = "library.json";
    #endregion

    #region Fields
    private static readonly string[] value = ["audio/*"];
    private readonly IAudioManager _audioManager;
    private List<AudioClip>? _cache;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly IPublicStorageService _publicStorageService;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the library service.
    ///</summary>
    ///<param fileName="audioManager">Used to probe a clip's duration when it's added.</param>
    ///<param fileName="publicStorageService">Used to export user-created clips into the shared Music folder.</param>
    public AudioLibraryService(IAudioManager audioManager, IPublicStorageService publicStorageService)
    {
        _audioManager = audioManager;
        _publicStorageService = publicStorageService;
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Loads the persisted catalog from disk into <see cref="_cache"/> on first use.
    ///</summary>
    private async Task EnsureLoadedAsync()
    {
        if (_cache is not null)
        {
            return;
        }

        await _lock.WaitAsync();
        try
        {
            if (_cache is not null)
            {
                return;
            }

            bool fileExists = File.Exists(LibraryFilePath);
            if (fileExists)
            {
                string json = await File.ReadAllTextAsync(LibraryFilePath);
                _cache = JsonSerializer.Deserialize<List<AudioClip>>(json) ?? [];
            }
            else
            {
                _cache = [];
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    ///<summary>
    ///Loads a file just long enough to read its duration; returns <see cref="TimeSpan.Zero"/> if that fails.
    ///</summary>
    private Task<TimeSpan> GetDurationAsync(string filePath)
    {
        try
        {
            using IAudioPlayer player = _audioManager.CreatePlayer(filePath);
            return Task.FromResult(TimeSpan.FromSeconds(player.Duration));
        }
        catch (Exception)
        {
            return Task.FromResult(TimeSpan.Zero);
        }
    }

    ///<summary>
    ///Persists the current in-memory catalog to disk as JSON.
    ///</summary>
    private async Task SaveAsync()
    {
        string json = JsonSerializer.Serialize(_cache);
        await File.WriteAllTextAsync(LibraryFilePath, json);
    }
    #endregion

    #region Private properties
    ///<summary>
    ///App-private folder where imported and picked audio files are copied.
    ///</summary>
    private static string ClipsDirectory
    {
        get
        {
            string dir = Path.Combine(FileSystem.AppDataDirectory, "Clips");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    ///<summary>
    ///Path to the JSON file backing the persisted library catalog.
    ///</summary>
    private static string LibraryFilePath => Path.Combine(FileSystem.AppDataDirectory, LibraryFileName);
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public async Task<AudioClip> AddClipAsync(string filePath, string name, bool isUserRecording = false)
    {
        TimeSpan duration = await GetDurationAsync(filePath);

        AudioClip clip = new() { Name = name, FilePath = filePath, Duration = duration, IsUserRecording = isUserRecording, };

        if (isUserRecording)
        {
            // Best-effort: a failed export to shared storage should never block saving to the library.
            clip.PublicStorageLocation = await _publicStorageService.ExportToMusicAsync(filePath, Path.GetFileName(filePath));
        }

        await EnsureLoadedAsync();
        _cache!.Add(clip);
        await SaveAsync();
        return clip;
    }

    ///<inheritdoc/>
    public async Task DeleteClipAsync(AudioClip clip)
    {
        await EnsureLoadedAsync();
        _cache!.RemoveAll(c => c.Id == clip.Id);
        await SaveAsync();

        try
        {
            if (File.Exists(clip.FilePath))
            {
                File.Delete(clip.FilePath);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; a locked or already-removed file shouldn't block the library update.
        }
    }

    ///<inheritdoc/>
    public async Task<IReadOnlyList<AudioClip>> GetClipsAsync()
    {
        await EnsureLoadedAsync();
        List<AudioClip> audioClips = [.. _cache!.OrderByDescending(c => c.ImportedAt)];
        return audioClips;
    }

    ///<inheritdoc/>
    public async Task<AudioClip?> ImportFromPickerAsync()
    {
        Dictionary<DevicePlatform, IEnumerable<string>> fileTypes = new() { { DevicePlatform.Android, value }, };
        FilePickerFileType audioFileType = new(fileTypes);

        PickOptions options = new() { PickerTitle = "Choose an audio file", FileTypes = audioFileType, };
        FileResult? result = await FilePicker.Default.PickAsync(options);

        if (result is null)
        {
            return null;
        }

        string path = $"{Guid.NewGuid():N}_{result.FileName}";
        string destination = Path.Combine(ClipsDirectory, path);
        await using (Stream source = await result.OpenReadAsync())
        {
            await using FileStream dest = File.Create(destination);
            await source.CopyToAsync(dest);
        }

        string fileName = Path.GetFileNameWithoutExtension(result.FileName);
        AudioClip audioClip = await AddClipAsync(destination, fileName);
        return audioClip;
    }

    ///<inheritdoc/>
    public async Task RenameClipAsync(AudioClip clip, string newName)
    {
        await EnsureLoadedAsync();
        AudioClip? existing = _cache!.FirstOrDefault(c => c.Id == clip.Id);
        if (existing is not null)
        {
            existing.Name = newName;
            await SaveAsync();
        }
    }
    #endregion
}
