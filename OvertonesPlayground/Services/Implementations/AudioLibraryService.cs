using System.Text.Json;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;
using Plugin.Maui.Audio;

namespace OvertonesPlayground.Services.Implementations;

///<inheritdoc cref="IAudioLibraryService"/>
public sealed class AudioLibraryService : IAudioLibraryService, IDisposable
{
    #region Constants
    ///<summary>
    ///Bytes read and written per step when copying a picked file; big enough that a ~300 MB file isn't hundreds of thousands
    ///of tiny writes.
    ///</summary>
    private const int CopyBufferSize = 1024 * 1024;

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
    ///Brings a picked file to <paramref name="destination"/>, reporting the fraction copied (0 to 1) to
    ///<paramref name="report"/>. Where possible it moves the copy the picker already made instead of copying it again.
    ///</summary>
    private async Task CopyPickedFileAsync(FileResult result, string destination, Action<double> report)
    {
        if (TryMovePickerCopy(result, destination))
        {
            report(1);
            return;
        }

        await using Stream source = await result.OpenReadAsync();
        await using FileStream dest = File.Create(destination);

        // Not every stream reports its length, in which case there is nothing to measure the copy against.
        long total = source.CanSeek ? source.Length : 0;
        byte[] buffer = new byte[CopyBufferSize];
        long copied = 0;
        int lastPercent = -1;
        int read;
        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, read));
            copied += read;

            int percent = total > 0 ? (int)Math.Min(100, copied * 100 / total) : lastPercent;
            bool hasAdvanced = percent != lastPercent;
            if (hasAdvanced)
            {
                lastPercent = percent;
                report(percent / 100.0);
            }
        }
    }

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
            // Double-checked locking: another caller may have populated the cache while we waited for the lock.
            // CA1508's single-threaded flow analysis can't see that, so it reports this guard as dead code.
#pragma warning disable CA1508
            if (_cache is not null)
            {
                return;
            }
#pragma warning restore CA1508

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

        await CopyPickedFileAsync(result, destination, fraction => progress?.Report(new ImportProgress(ImportStage.Copying, fraction * copyShare)));

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

    ///<summary>
    ///On Android the picker copies what the user chose into the app's cache folder before handing it back. If that is
    ///what <paramref name="result"/> points at, moves it to <paramref name="destination"/> (a rename, however big the
    ///file) and returns true. A file anywhere else may be the user's own, so it is never moved; returns false and leaves
    ///it alone.
    ///</summary>
    private bool TryMovePickerCopy(FileResult result, string destination)
    {
        string? pickedPath = result.FullPath;
        bool isMissing = string.IsNullOrEmpty(pickedPath) || !File.Exists(pickedPath);
        if (isMissing)
        {
            return false;
        }

        try
        {
            // The picker and FileSystem.CacheDirectory can name the same folder differently (/data/data/... and
            // /data/user/0/...), so compare the folders once their links are followed.
            string cacheFolder = ResolveFolderLinks(_fileSystem.CacheDirectory) + Path.DirectorySeparatorChar;
            bool isPickerCopy = ResolveFolderLinks(pickedPath).StartsWith(cacheFolder, StringComparison.Ordinal);
            if (!isPickerCopy)
            {
                return false;
            }

            File.Move(pickedPath, destination);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fall back to copying the stream.
            return false;
        }
    }

    ///<summary>
    ///Returns <paramref name="path"/> with any symbolic link among its folders followed, so that two spellings of the same
    ///location come out identical. On Android /data/data and /data/user/0 are the same folder that way.
    ///</summary>
    private static string ResolveFolderLinks(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? folder = Path.GetDirectoryName(fullPath);
        while (!string.IsNullOrEmpty(folder))
        {
            // A drive or filesystem root is never a link, and on Windows asking one to resolve throws.
            bool isRoot = string.Equals(folder, Path.GetPathRoot(folder), StringComparison.Ordinal);
            if (isRoot)
            {
                break;
            }

            FileSystemInfo? target = new DirectoryInfo(folder).ResolveLinkTarget(returnFinalTarget: true);
            if (target is not null)
            {
                return Path.Combine(target.FullName, Path.GetRelativePath(folder, fullPath));
            }

            folder = Path.GetDirectoryName(folder);
        }

        return fullPath;
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

    ///<summary>Disposes the semaphore that serialises catalog access.</summary>
    public void Dispose() => _lock.Dispose();
    #endregion

    #region Nested types
    ///<summary>
    ///An <see cref="IProgress{T}"/> that runs its callback on whichever thread reports, unlike <see cref="Progress{T}"/>,
    ///which posts each report to a thread pool thread when it wasn't created on a UI thread and so can deliver them out of
    ///order.
    ///</summary>
    private sealed class SyncProgress(Action<double> onReport) : IProgress<double>
    {
        public void Report(double value) => onReport(value);
    }
    #endregion
}
