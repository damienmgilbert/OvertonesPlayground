using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for generating simple synth tones. "Generate &amp; Play" only previews the sound; it isn't kept until the
///user explicitly taps Save.
///</summary>
public partial class ToneGeneratorViewModel : BaseViewModel
{
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
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Synthesizes the current settings, previews the result, and enables Save.
    ///</summary>
    [RelayCommand]
    private async Task GenerateAsync()
    {
        if(IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            string name = $"{SelectedWaveform} {FrequencyHz:0}Hz";
            Logger.LogDebug("Generating tone '{Name}' ({DurationSeconds}s, amplitude {Amplitude}).", name, DurationSeconds, Amplitude);
            _pendingClip = await _synthesisService.GenerateToneAsync(SelectedWaveform, FrequencyHz, DurationSeconds, Amplitude, name);

            WaveformPeaks = await _editorService.GetWaveformPeaksAsync(_pendingClip.FilePath, 300);
            await _playbackService.LoadAsync(_pendingClip);
            _playbackService.Play();

            CanSave = true;
            StatusMessage = "Previewing - tap Save to keep it in your library.";
        } finally
        {
            IsBusy = false;
        }
    }

    ///<summary>
    ///Persists the pending preview clip into the library (and, best-effort, the shared Music folder).
    ///</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if(_pendingClip is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Logger.LogDebug("Saving generated clip '{ClipName}'.", _pendingClip.Name);
            AudioClip saved = await _libraryService.AddClipAsync(_pendingClip.FilePath, _pendingClip.Name, isUserRecording: true);
            StatusMessage = saved.PublicStorageLocation is { } location ? $"Saved '{saved.Name}' - also in {location}." : $"Saved '{saved.Name}' to your library.";

            _pendingClip = null;
            CanSave = false;
        } finally
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
        Logger.LogDebug("Selected preset '{PresetName}'.", preset.Name);
        SelectedWaveform = preset.Waveform;
        FrequencyHz = preset.FrequencyHz;
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
