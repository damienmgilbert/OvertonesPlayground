using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

[QueryProperty(nameof(ClipId), "clipId")]
public partial class AudioEditorViewModel : BaseViewModel
{
    private readonly IAudioEditorService _editorService;
    private readonly IAudioLibraryService _libraryService;
    private readonly IAudioPlaybackService _playbackService;

    [ObservableProperty]
    public partial string? ClipId { get; set; }

    [ObservableProperty]
    public partial AudioClip? LoadedClip { get; set; }

    [ObservableProperty]
    public partial float[] WaveformPeaks { get; set; } = [];

    [ObservableProperty]
    public partial double DurationSeconds { get; set; }

    [ObservableProperty]
    public partial double TrimStartSeconds { get; set; }

    [ObservableProperty]
    public partial double TrimEndSeconds { get; set; }

    [ObservableProperty]
    public partial double GainDb { get; set; }

    [ObservableProperty]
    public partial double FadeInSeconds { get; set; } = 0.5;

    [ObservableProperty]
    public partial double FadeOutSeconds { get; set; } = 0.5;

    public AudioEditorViewModel(
        IAudioEditorService editorService,
        IAudioLibraryService libraryService,
        IAudioPlaybackService playbackService)
    {
        _editorService = editorService;
        _libraryService = libraryService;
        _playbackService = playbackService;
        Title = "Sound Editor";
    }

    partial void OnClipIdChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _ = LoadClipAsync(value);
        }
    }

    private async Task LoadClipAsync(string clipId)
    {
        IsBusy = true;
        try
        {
            var clips = await _libraryService.GetClipsAsync();
            var clip = clips.FirstOrDefault(c => c.Id == clipId);
            if (clip is null)
            {
                StatusMessage = "Could not find that clip.";
                return;
            }

            await SetLoadedClipAsync(clip);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SetLoadedClipAsync(AudioClip clip)
    {
        LoadedClip = clip;
        DurationSeconds = clip.Duration.TotalSeconds;
        TrimStartSeconds = 0;
        TrimEndSeconds = DurationSeconds;
        WaveformPeaks = await _editorService.GetWaveformPeaksAsync(clip.FilePath, 400);
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (LoadedClip is null)
        {
            return;
        }

        await _playbackService.LoadAsync(LoadedClip);
        _playbackService.Play();
    }

    [RelayCommand]
    private async Task TrimAsync()
    {
        await ApplyEditAsync(clip => _editorService.TrimAsync(
            clip.FilePath,
            TimeSpan.FromSeconds(TrimStartSeconds),
            TimeSpan.FromSeconds(TrimEndSeconds),
            "trim"));
    }

    [RelayCommand]
    private async Task ApplyGainAsync() =>
        await ApplyEditAsync(clip => _editorService.ApplyGainAsync(clip.FilePath, GainDb, "gain"));

    [RelayCommand]
    private async Task ApplyFadeAsync() =>
        await ApplyEditAsync(clip => _editorService.ApplyFadeAsync(
            clip.FilePath,
            TimeSpan.FromSeconds(FadeInSeconds),
            TimeSpan.FromSeconds(FadeOutSeconds),
            "fade"));

    [RelayCommand]
    private async Task ReverseAsync() =>
        await ApplyEditAsync(clip => _editorService.ReverseAsync(clip.FilePath, "reverse"));

    [RelayCommand]
    private async Task NormalizeAsync() =>
        await ApplyEditAsync(clip => _editorService.NormalizeAsync(clip.FilePath, "normalize"));

    private async Task ApplyEditAsync(Func<AudioClip, Task<string>> operation)
    {
        if (LoadedClip is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var outputPath = await operation(LoadedClip);
            var newClip = await _libraryService.AddClipAsync(outputPath, $"{LoadedClip.Name} (edited)");
            await SetLoadedClipAsync(newClip);
            StatusMessage = $"Saved as '{newClip.Name}' in your library.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
