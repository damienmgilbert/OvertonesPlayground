using System.Text.Json;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;
using Plugin.Maui.Audio;

namespace OvertonesPlayground.Services.Implementations;

///<inheritdoc cref="IAudioLibraryService"/>
public class AudioLibraryService : IAudioLibraryService
{
    #region Constants
    ///<summary>
    ///The share of the whole import (0 to 1) taken by bringing the file in when it will then be decoded. Decoding is far
    ///slower than copying, so it gets most of the bar.
    ///</summary>
    private const double CopyShareWhenDecoding = 0.1;

    ///<summary>
    ///The share of the whole import (0 to 1) taken by bringing the file in when nothing else has to be done to it.
    ///</summary>
    private const double CopyShareWhenNotDecoding = 0.95;

    ///<summary>
    ///Where the import is up to (0 to 1) once the file is ready and only the library entry is left.
    ///</summary>
    private const double FinishingStart = 0.95;

    private const string LibraryFileName = "library.json";
    #endregion

    #region Fields
    private static readonly string[] value = ["audio/*"];
    private readonly IAudioManager _audioManager;
    private List<AudioClip>? _cache;
    private readonly IFilePicker _filePicker;
    private readonly IFileSystem _fileSystem;
    private readonly IAudioFormatConverterService _formatConverterService;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly IPublicStorageService _publicStorageService;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the library service.
    ///</summary>
    ///<param fileName="audioManager">Used to probe a clip's duration when it's added.</param>
    ///<param fileName="formatConverterService">Used to decode a picked non-WAV file (e.g. MP3) before it's added, so
    ///the app's WAV-only pipeline can read it.</param>
    ///<param fileName="publicStorageService">Used to export user-created clips into the shared Music folder.</param>
    ///<param fileName="fileSystem">Locates the app-private folders the catalog and imported files live in, and the cache
    ///folder the picker copies chosen files into.</param>
    ///<param fileName="filePicker">Shows the system file picker when the user imports a clip.</param>
    public AudioLibraryService(IAudioManager audioManager, IAudioFormatConverterService formatConverterService, IPublicStorageService publicStorageService, IFileSystem fileSystem, IFilePicker filePicker)
    {
        _audioManager = audioManager;
        _formatConverterService = formatConverterService;
        _publicStorageService = publicStorageService;
        _fileSystem = fileSystem;
        _filePicker = filePicker;
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

    ///<summary>
    ///Gets a picked file into app storage and, if it isn't a WAV, decodes it, so that what comes back is a WAV the rest of
    ///the app can read. Reports overall import progress as it goes, always in order, because it runs on a single thread.
    ///</summary>
    private async Task<string> StageFileAsync(FileResult result, string destination, string clipName, IProgress<ImportProgress>? progress)
    {
        bool needsConversion = _formatConverterService.NeedsConversion(destination);
        double copyShare = needsConversion ? CopyShareWhenDecoding : CopyShareWhenNotDecoding;

        await PickedFileStager.StageAsync(result, destination, _fileSystem, fraction => progress?.Report(new ImportProgress(ImportStage.Copying, fraction * copyShare)));

        if (!needsConversion)
        {
            return destination;
        }

        SyncProgress decodeProgress = new(fraction => progress?.Report(new ImportProgress(ImportStage.Decoding, copyShare + (fraction * (FinishingStart - copyShare)))));
        string wavPath = await _formatConverterService.ConvertToWavAsync(destination, clipName, decodeProgress);
        try
        {
            File.Delete(destination);
        }
        catch (IOException)
        {
            // Best-effort cleanup; the converted WAV is what matters, not removing the original compressed copy.
        }

        return wavPath;
    }
    #endregion

    #region Private properties
    ///<summary>
    ///App-private folder where imported and picked audio files are copied.
    ///</summary>
    private string ClipsDirectory
    {
        get
        {
            string dir = Path.Combine(_fileSystem.AppDataDirectory, "Clips");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    ///<summary>
    ///Path to the JSON file backing the persisted library catalog.
    ///</summary>
    private string LibraryFilePath => Path.Combine(_fileSystem.AppDataDirectory, LibraryFileName);
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
    public async Task<string?> ExportClipAsync(AudioClip clip, AudioExportFormat format)
    {
        ArgumentNullException.ThrowIfNull(clip);

        string encodedPath = await _formatConverterService.ConvertFromWavAsync(clip.FilePath, format, clip.Name);
        return await _publicStorageService.ExportToMusicAsync(encodedPath, Path.GetFileName(encodedPath));
    }

    ///<inheritdoc/>
    public async Task<IReadOnlyList<AudioClip>> GetClipsAsync()
    {
        await EnsureLoadedAsync();
        List<AudioClip> audioClips = [.. _cache!.OrderByDescending(c => c.ImportedAt)];
        return audioClips;
    }

    ///<inheritdoc/>
    public async Task<AudioClip?> ImportFromPickerAsync(IProgress<ImportProgress>? progress = null)
    {
        Dictionary<DevicePlatform, IEnumerable<string>> fileTypes = new() { { DevicePlatform.Android, value }, };
        FilePickerFileType audioFileType = new(fileTypes);

        PickOptions options = new() { PickerTitle = "Choose an audio file", FileTypes = audioFileType, };
        FileResult? result = await _filePicker.PickAsync(options);

        if (result is null)
        {
            return null;
        }

        string path = $"{Guid.NewGuid():N}_{result.FileName}";
        string destination = Path.Combine(ClipsDirectory, path);
        string fileName = Path.GetFileNameWithoutExtension(result.FileName);

        // Copying and decoding a large file take long enough to freeze the UI (and so the progress bar), so they run on a
        // pool thread. Awaiting from here puts the rest back on the caller's thread.
        string clipPath = await Task.Run(() => StageFileAsync(result, destination, fileName, progress));

        progress?.Report(new ImportProgress(ImportStage.Finishing, FinishingStart));
        AudioClip audioClip = await AddClipAsync(clipPath, fileName);
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
