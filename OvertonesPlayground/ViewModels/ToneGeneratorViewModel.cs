using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for generating simple synth tones. "Generate &amp; Play" only previews the sound; it isn't kept until the
///user explicitly taps Save.
///</summary>
public partial class ToneGeneratorViewModel : BaseViewModel
{
    #region Constants
    // The manual controls' ranges. The page binds its sliders to these, and every preset must fit inside them: a slider
    // clamps whatever it is given and writes the clamped value back here, so a preset outside a range would silently
    // generate a different sound from the one it names (the "Click" preset once came out at 2000 Hz and 0.10 s).

    ///<summary>
    ///Highest frequency the Frequency control accepts, in Hz.
    ///</summary>
    public const double MaxFrequencyHz = 4000;

    ///<summary>
    ///Longest tone the Duration control accepts, in seconds.
    ///</summary>
    public const double MaxDurationSeconds = 3;

    ///<summary>
    ///Lowest frequency the Frequency control accepts, in Hz.
    ///</summary>
    public const double MinFrequencyHz = 20;

    ///<summary>
    ///Shortest tone the Duration control accepts, in seconds.
    ///</summary>
    public const double MinDurationSeconds = 0.05;
    #endregion

    #region Fields
    private readonly IAudioEditorService _editorService;
    private readonly IAudioLibraryService _libraryService;
    ///<summary>
    ///The most recently generated, not-yet-saved preview clip; null once saved or before the first Generate.
    ///</summary>
    private AudioClip? _pendingClip;
    private readonly IAudioPlaybackService _playbackService;
    private readonly ISoundSynthesisService _synthesisService;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the view model with its default sine-wave settings.
    ///</summary>
    public ToneGeneratorViewModel(ISoundSynthesisService synthesisService, IAudioEditorService editorService, IAudioPlaybackService playbackService, IAudioLibraryService libraryService, ILogger<ToneGeneratorViewModel> logger) : base(logger)
    {
        _synthesisService = synthesisService;
        _editorService = editorService;
        _playbackService = playbackService;
        _libraryService = libraryService;
        Title = "Tone Generator";

        AssertPresetsFitControls();
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Forgets the preview once the controls no longer match it, so Save can't store a sound other than the one set up.
    ///</summary>
    private void DiscardStalePreview()
    {
        if (_pendingClip is null)
        {
            return;
        }

        _pendingClip = null;
        CanSave = false;
        StatusMessage = "Settings changed - tap Generate & Play to hear it before saving.";
    }

    partial void OnAmplitudeChanged(double value) => DiscardStalePreview();

    partial void OnDurationSecondsChanged(double value) => DiscardStalePreview();

    partial void OnFrequencyHzChanged(double value) => DiscardStalePreview();

    partial void OnSelectedWaveformChanged(WaveformType value)
    {
        OnPropertyChanged(nameof(IsFrequencyRelevant));
        DiscardStalePreview();
    }

    ///<summary>
    ///Synthesizes the current settings, previews the result, and enables Save.
    ///</summary>
    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            string name = IsFrequencyRelevant ? $"{SelectedWaveform} {FrequencyHz:0}Hz" : $"{SelectedWaveform}";
            Log_GeneratingTone(name, DurationSeconds, Amplitude);
            _pendingClip = await _synthesisService.GenerateToneAsync(SelectedWaveform, FrequencyHz, DurationSeconds, Amplitude, name);

            WaveformPeaks = await _editorService.GetWaveformPeaksAsync(_pendingClip.FilePath, 300);
            await _playbackService.LoadAsync(_pendingClip);
            _playbackService.Play();

            CanSave = true;
            StatusMessage = "Previewing - tap Save to keep it in your library.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            Log_GenerateFailed(ex, SelectedWaveform);
            StatusMessage = "Couldn't generate that tone.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    ///<summary>
    ///Debug builds only: fails loudly if a preset doesn't fit the manual controls' ranges, so a future preset can't be
    ///silently clamped the way "Click" once was.
    ///</summary>
    [Conditional("DEBUG")]
    private static void AssertPresetsFitControls()
    {
        foreach (TonePreset preset in TonePreset.All)
        {
            bool isNoise = preset.Waveform is WaveformType.WhiteNoise or WaveformType.PinkNoise;
            bool frequencyFits = isNoise || preset.FrequencyHz is >= MinFrequencyHz and <= MaxFrequencyHz;
            bool durationFits = preset.DurationSeconds is >= MinDurationSeconds and <= MaxDurationSeconds;
            bool amplitudeFits = preset.Amplitude is >= 0 and <= 1;
            Debug.Assert(frequencyFits && durationFits && amplitudeFits, $"Preset '{preset.Name}' is outside the slider ranges, so the sliders would clamp it.");
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to generate a {Waveform} tone.")]
    private partial void Log_GenerateFailed(Exception exception, WaveformType waveform);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generating tone '{Name}' ({DurationSeconds}s, amplitude {Amplitude}).")]
    private partial void Log_GeneratingTone(string name, double durationSeconds, double amplitude);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save the generated clip.")]
    private partial void Log_SaveFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saving generated clip '{ClipName}'.")]
    private partial void Log_SavingClip(string clipName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Selected preset '{PresetName}'.")]
    private partial void Log_SelectedPreset(string presetName);

    ///<summary>
    ///Persists the pending preview clip into the library (and, best-effort, the shared Music folder).
    ///</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_pendingClip is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Log_SavingClip(_pendingClip.Name);
            AudioClip saved = await _libraryService.AddClipAsync(_pendingClip.FilePath, _pendingClip.Name, isUserRecording: true);
            StatusMessage = saved.PublicStorageLocation is { } location ? $"Saved '{saved.Name}' - also in {location}." : $"Saved '{saved.Name}' to your library.";

            _pendingClip = null;
            CanSave = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log_SaveFailed(ex);
            StatusMessage = "Couldn't save that tone to your library.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    ///<summary>
    ///Loads a preset's waveform, frequency, duration, and level into the manual controls.
    ///</summary>
    [RelayCommand]
    private void SelectPreset(TonePreset preset)
    {
        Log_SelectedPreset(preset.Name);
        SelectedWaveform = preset.Waveform;
        if (IsFrequencyRelevant)
        {
            FrequencyHz = preset.FrequencyHz;
        }

        DurationSeconds = preset.DurationSeconds;
        Amplitude = preset.Amplitude;
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Output level from 0 (silent) to 1 (full scale).
    ///</summary>
    [ObservableProperty]
    public partial double Amplitude { get; set; } = 0.8;

    ///<summary>
    ///Whether there's a generated preview waiting to be saved.
    ///</summary>
    [ObservableProperty]
    public partial bool CanSave { get; set; }

    ///<summary>
    ///Length of the generated tone in seconds.
    ///</summary>
    [ObservableProperty]
    public partial double DurationSeconds { get; set; } = 1.0;

    ///<summary>
    ///Oscillator frequency in Hz.
    ///</summary>
    [ObservableProperty]
    public partial double FrequencyHz { get; set; } = 440;

    ///<summary>
    ///False for the noise waveforms, which have no pitch, so the Frequency control has no effect on them.
    ///</summary>
    public bool IsFrequencyRelevant => SelectedWaveform is not (WaveformType.WhiteNoise or WaveformType.PinkNoise);

    ///<summary>
    ///Quick-select presets shown above the manual controls.
    ///</summary>
    public IReadOnlyList<TonePreset> Presets { get; } = TonePreset.All;

    ///<summary>
    ///The oscillator/noise type to generate.
    ///</summary>
    [ObservableProperty]
    public partial WaveformType SelectedWaveform { get; set; } = WaveformType.Sine;

    ///<summary>
    ///Every waveform type the Wave picker can select from.
    ///</summary>
    public IReadOnlyList<WaveformType> WaveformOptions { get; } = Enum.GetValues<WaveformType>();

    ///<summary>
    ///Waveform peaks for the most recently generated preview, used to draw the waveform view.
    ///</summary>
    [ObservableProperty]
    public partial float[] WaveformPeaks { get; set; } = [];
    #endregion
}
