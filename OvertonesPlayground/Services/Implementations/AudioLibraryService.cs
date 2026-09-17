using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;
using Plugin.Maui.Audio;
using System.Text.Json;

namespace OvertonesPlayground.Services.Implementations;

public class AudioLibraryService : IAudioLibraryService
{
    private const string LibraryFileName = "library.json";

    private readonly IAudioManager _audioManager;
    private readonly IPublicStorageService _publicStorageService;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<AudioClip>? _cache;

    public AudioLibraryService(IAudioManager audioManager, IPublicStorageService publicStorageService)
    {
        _audioManager = audioManager;
        _publicStorageService = publicStorageService;
    }

    private static string ClipsDirectory
    {
        get
        {
            var dir = Path.Combine(FileSystem.AppDataDirectory, "Clips");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string LibraryFilePath => Path.Combine(FileSystem.AppDataDirectory, LibraryFileName);

    public async Task<IReadOnlyList<AudioClip>> GetClipsAsync()
    {
        await EnsureLoadedAsync();
        List<AudioClip> audioClips = [.. _cache!.OrderByDescending(c => c.ImportedAt)];
        return audioClips;
    }

    public async Task<AudioClip?> ImportFromPickerAsync()
    {
        var audioFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.Android, new[] { "audio/*" } },
        });

        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Choose an audio file",
            FileTypes = audioFileType,
        });

        if (result is null)
        {
            return null;
        }

        var destination = Path.Combine(ClipsDirectory, $"{Guid.NewGuid():N}_{result.FileName}");
        await using (var source = await result.OpenReadAsync())
        await using (var dest = File.Create(destination))
        {
            await source.CopyToAsync(dest);
        }

        return await AddClipAsync(destination, Path.GetFileNameWithoutExtension(result.FileName));
    }

    public async Task<AudioClip> AddClipAsync(string filePath, string name, bool isUserRecording = false)
    {
        var duration = await GetDurationAsync(filePath);

        var clip = new AudioClip
        {
            Name = name,
            FilePath = filePath,
            Duration = duration,
            IsUserRecording = isUserRecording,
        };

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

    public async Task RenameClipAsync(AudioClip clip, string newName)
    {
        await EnsureLoadedAsync();
        var existing = _cache!.FirstOrDefault(c => c.Id == clip.Id);
        if (existing is not null)
        {
            existing.Name = newName;
            await SaveAsync();
        }
    }

    private Task<TimeSpan> GetDurationAsync(string filePath)
    {
        try
        {
            using var player = _audioManager.CreatePlayer(filePath);
            return Task.FromResult(TimeSpan.FromSeconds(player.Duration));
        }
        catch (Exception)
        {
            return Task.FromResult(TimeSpan.Zero);
        }
    }

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

            if (File.Exists(LibraryFilePath))
            {
                var json = await File.ReadAllTextAsync(LibraryFilePath);
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

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_cache);
        await File.WriteAllTextAsync(LibraryFilePath, json);
    }
}
