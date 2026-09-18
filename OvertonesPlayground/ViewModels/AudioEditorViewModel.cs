using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        if (LoadedClip is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Log_ApplyingEdit(operationName, LoadedClip.Name);
            string outputPath = await operation(LoadedClip);
            AudioClip newClip = await _libraryService.AddClipAsync(outputPath, $"{LoadedClip.Name} (edited)", isUserRecording: true);
            await SetLoadedClipAsync(newClip);
            StatusMessage = newClip.PublicStorageLocation is { } location ? $"Saved as '{newClip.Name}' - also in {location}." : $"Saved as '{newClip.Name}' in your library.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            Log_EditFailed(ex, operationName, LoadedClip.Name);
            StatusMessage = $"Couldn't apply that {operationName}.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyCompressionAsync() { await ApplyEditAsync("compression", clip => _editorService.ApplyCompressionAsync(clip.FilePath, CompressionThresholdDb, CompressionRatio, CompressionAttackMs, CompressionReleaseMs, "compress")); }
    [RelayCommand]
    private async Task ApplyEqualizerAsync() { await ApplyEditAsync("EQ", clip => _editorService.ApplyEqualizerAsync(clip.FilePath, EqLowGainDb, EqMidGainDb, EqHighGainDb, "eq")); }
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
            if (clip is null)
            {
                Log_ClipNotFound(clipId);
                StatusMessage = "Could not find that clip.";
                return;
            }

            Log_LoadingClip(clip.Name, clipId);
            await SetLoadedClipAsync(clip);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            Log_LoadClipFailed(ex, clipId);
            StatusMessage = "Couldn't load that clip.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Applying '{Operation}' to clip '{ClipName}'.")]
    private partial void Log_ApplyingEdit(string operation, string clipName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Clip {ClipId} not found.")]
    private partial void Log_ClipNotFound(string clipId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to apply '{Operation}' to clip '{ClipName}'.")]
    private partial void Log_EditFailed(Exception exception, string operation, string clipName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load clip {ClipId} into editor.")]
    private partial void Log_LoadClipFailed(Exception exception, string clipId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading clip '{ClipName}' ({ClipId}) into editor.")]
    private partial void Log_LoadingClip(string clipName, string clipId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Opening the trim editor for clip '{ClipName}'.")]
    private partial void Log_OpeningTrimEditor(string clipName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Previewing clip '{ClipName}'.")]
    private partial void Log_PreviewingClip(string clipName);

    [RelayCommand]
    private async Task NormalizeAsync() { await ApplyEditAsync("normalize", clip => _editorService.NormalizeAsync(clip.FilePath, "normalize")); }

    partial void OnClipIdChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _ = LoadClipAsync(value);
        }
    }

    [RelayCommand]
    private async Task OpenTrimEditorAsync()
    {
        if (LoadedClip is null)
        {
            return;
        }

        Log_OpeningTrimEditor(LoadedClip.Name);
        await Shell.Current.GoToAsync($"trim?clipId={LoadedClip.Id}");
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (LoadedClip is null)
        {
            return;
        }

        Log_PreviewingClip(LoadedClip.Name);
        await _playbackService.LoadAsync(LoadedClip);
        _playbackService.Play();
    }

    [RelayCommand]
    private async Task ReduceNoiseAsync() { await ApplyEditAsync("noise reduction", clip => _editorService.ReduceNoiseAsync(clip.FilePath, TimeSpan.FromSeconds(NoiseSampleSeconds), "denoise")); }

    [RelayCommand]
    private async Task RemoveVocalsAsync() { await ApplyEditAsync("vocal removal", clip => _editorService.RemoveVocalsAsync(clip.FilePath, "no-vocals")); }

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
    ///How fast (milliseconds) the compressor's envelope follows a rising level before gain reduction kicks in.
    ///</summary>
    [ObservableProperty]
    public partial double CompressionAttackMs { get; set; } = 10;

    ///<summary>
    ///How much gain above threshold is reduced - e.g. 4 means 4 dB in becomes 1 dB out.
    ///</summary>
    [ObservableProperty]
    public partial double CompressionRatio { get; set; } = 4;

    ///<summary>
    ///How fast (milliseconds) the compressor's envelope follows a falling level, releasing gain reduction.
    ///</summary>
    [ObservableProperty]
    public partial double CompressionReleaseMs { get; set; } = 100;

    ///<summary>
    ///Level (dB) above which the compressor starts reducing gain.
    ///</summary>
    [ObservableProperty]
    public partial double CompressionThresholdDb { get; set; } = -18;

    ///<summary>
    ///Duration of the loaded clip in seconds.
    ///</summary>
    [ObservableProperty]
    public partial double DurationSeconds { get; set; }

    ///<summary>
    ///Equalizer high shelf gain (dB, ~6 kHz and up).
    ///</summary>
    [ObservableProperty]
    public partial double EqHighGainDb { get; set; }

    ///<summary>
    ///Equalizer low shelf gain (dB, ~150 Hz and below).
    ///</summary>
    [ObservableProperty]
    public partial double EqLowGainDb { get; set; }

    ///<summary>
    ///Equalizer mid peaking-band gain (dB, centered ~1 kHz).
    ///</summary>
    [ObservableProperty]
    public partial double EqMidGainDb { get; set; }

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
    ///Length (seconds), taken from the start of the clip, used to estimate the noise profile for noise reduction.
    ///Trim the clip to noise-only audio first for the best result.
    ///</summary>
    [ObservableProperty]
    public partial double NoiseSampleSeconds { get; set; } = 0.5;

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
