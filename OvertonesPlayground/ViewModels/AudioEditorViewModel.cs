using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for the audio editor page. Loads a selected <see cref="OvertonesPlayground.Models.AudioClip"/> and exposes
///commands to preview and perform simple edits (trim, fade, normalize, reverse).
///</summary>
[QueryProperty(nameof(ClipId), "clipId")]
public partial class AudioEditorViewModel : BaseViewModel
{
    #region Fields
    private readonly IAudioEditorService _editorService;
    private readonly IAudioLibraryService _libraryService;
    private readonly IAudioPlaybackService _playbackService;
    #endregion

    #region Constructors
    public AudioEditorViewModel(IAudioEditorService editorService, IAudioLibraryService libraryService, IAudioPlaybackService playbackService, ILogger<AudioEditorViewModel> logger) : base(logger)
    {
        _editorService = editorService;
        _libraryService = libraryService;
        _playbackService = playbackService;
        Title = "Sound Editor";
    }
    #endregion

    #region Private methods
    private async Task ApplyEditAsync(string operationName, Func<AudioClip, Task<string>> operation)
    {
        if(LoadedClip is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Logger.LogDebug("Applying '{Operation}' to clip '{ClipName}'.", operationName, LoadedClip.Name);
            string outputPath = await operation(LoadedClip);
            AudioClip newClip = await _libraryService.AddClipAsync(outputPath, $"{LoadedClip.Name} (edited)", isUserRecording: true);
            await SetLoadedClipAsync(newClip);
            StatusMessage = newClip.PublicStorageLocation is { } location ? $"Saved as '{newClip.Name}' - also in {location}." : $"Saved as '{newClip.Name}' in your library.";
        } finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyFadeAsync() { await ApplyEditAsync("fade", clip => _editorService.ApplyFadeAsync(clip.FilePath, TimeSpan.FromSeconds(FadeInSeconds), TimeSpan.FromSeconds(FadeOutSeconds), "fade")); }
    [RelayCommand]
    private async Task ApplyGainAsync() { await ApplyEditAsync("gain", clip => _editorService.ApplyGainAsync(clip.FilePath, GainDb, "gain")); }

    private async Task LoadClipAsync(string clipId)
    {
        IsBusy = true;
        try
        {
            IReadOnlyList<AudioClip> clips = await _libraryService.GetClipsAsync();
            AudioClip? clip = clips.FirstOrDefault(c => c.Id == clipId);
            if(clip is null)
            {
                Logger.LogDebug("Clip {ClipId} not found.", clipId);
                StatusMessage = "Could not find that clip.";
                return;
            }

            Logger.LogDebug("Loading clip '{ClipName}' ({ClipId}) into editor.", clip.Name, clipId);
            await SetLoadedClipAsync(clip);
        } finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NormalizeAsync() { await ApplyEditAsync("normalize", clip => _editorService.NormalizeAsync(clip.FilePath, "normalize")); }

    partial void OnClipIdChanged(string? value)
    {
        if(!string.IsNullOrEmpty(value))
        {
            _ = LoadClipAsync(value);
        }
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if(LoadedClip is null)
        {
            return;
        }

        Logger.LogDebug("Previewing clip '{ClipName}'.", LoadedClip.Name);
        await _playbackService.LoadAsync(LoadedClip);
        _playbackService.Play();
    }

    [RelayCommand]
    private async Task ReverseAsync() { await ApplyEditAsync("reverse", clip => _editorService.ReverseAsync(clip.FilePath, "reverse")); }

    private async Task SetLoadedClipAsync(AudioClip clip)
    {
        LoadedClip = clip;
        DurationSeconds = clip.Duration.TotalSeconds;
        TrimStartSeconds = 0;
        TrimEndSeconds = DurationSeconds;
        WaveformPeaks = await _editorService.GetWaveformPeaksAsync(clip.FilePath, 400);
    }

    [RelayCommand]
    private async Task TrimAsync() { await ApplyEditAsync("trim", clip => _editorService.TrimAsync(clip.FilePath, TimeSpan.FromSeconds(TrimStartSeconds), TimeSpan.FromSeconds(TrimEndSeconds), "trim")); }
    #endregion

    #region Public properties
    ///<summary>
    ///Id of the clip to load (set via query property when navigating to the editor).
    ///</summary>
    [ObservableProperty]
    public partial string? ClipId { get; set; }

    ///<summary>
    ///Duration of the loaded clip in seconds.
    ///</summary>
    [ObservableProperty]
    public partial double DurationSeconds { get; set; }

    ///<summary>
    ///Fade-in duration in seconds when applying fades.
    ///</summary>
    [ObservableProperty]
    public partial double FadeInSeconds { get; set; } = 0.5;

    ///<summary>
    ///Fade-out duration in seconds when applying fades.
    ///</summary>
    [ObservableProperty]
    public partial double FadeOutSeconds { get; set; } = 0.5;

    ///<summary>
    ///Gain to apply (dB) when applying a gain edit.
    ///</summary>
    [ObservableProperty]
    public partial double GainDb { get; set; }

    ///<summary>
    ///The clip currently loaded into the editor.
    ///</summary>
    [ObservableProperty]
    public partial AudioClip? LoadedClip { get; set; }

    ///<summary>
    ///End position (seconds) for trimming operations.
    ///</summary>
    [ObservableProperty]
    public partial double TrimEndSeconds { get; set; }

    ///<summary>
    ///Start position (seconds) for trimming operations.
    ///</summary>
    [ObservableProperty]
    public partial double TrimStartSeconds { get; set; }

    ///<summary>
    ///Preview waveform peaks used by the UI to draw an overview.
    ///</summary>
    [ObservableProperty]
    public partial float[] WaveformPeaks { get; set; } = [];
    #endregion
}
