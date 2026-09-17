using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

/// <summary>
/// View model for generating simple synth tones. "Generate &amp; Play" only previews the sound;
/// it isn't kept until the user explicitly taps Save.
/// </summary>
public partial class ToneGeneratorViewModel : BaseViewModel
{
    private readonly ISoundSynthesisService _synthesisService;
    private readonly IAudioEditorService _editorService;
    private readonly IAudioPlaybackService _playbackService;
    private readonly IAudioLibraryService _libraryService;

    private AudioClip? _pendingClip;

    [ObservableProperty]
    public partial WaveformType SelectedWaveform { get; set; } = WaveformType.Sine;

    [ObservableProperty]
    public partial double FrequencyHz { get; set; } = 440;

    [ObservableProperty]
    public partial double DurationSeconds { get; set; } = 1.0;

    [ObservableProperty]
    public partial double Amplitude { get; set; } = 0.8;

    [ObservableProperty]
    public partial float[] WaveformPeaks { get; set; } = [];

    [ObservableProperty]
    public partial bool CanSave { get; set; }

    public IReadOnlyList<WaveformType> WaveformOptions { get; } = Enum.GetValues<WaveformType>();

    public IReadOnlyList<TonePreset> Presets { get; } = TonePreset.All;

    public ToneGeneratorViewModel(
        ISoundSynthesisService synthesisService,
        IAudioEditorService editorService,
        IAudioPlaybackService playbackService,
        IAudioLibraryService libraryService)
    {
        _synthesisService = synthesisService;
        _editorService = editorService;
        _playbackService = playbackService;
        _libraryService = libraryService;
        Title = "Tone Generator";
    }

    [RelayCommand]
    private void SelectPreset(TonePreset preset)
    {
        SelectedWaveform = preset.Waveform;
        FrequencyHz = preset.FrequencyHz;
        DurationSeconds = preset.DurationSeconds;
        Amplitude = preset.Amplitude;
    }

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
            var name = $"{SelectedWaveform} {FrequencyHz:0}Hz";
            _pendingClip = await _synthesisService.GenerateToneAsync(
                SelectedWaveform, FrequencyHz, DurationSeconds, Amplitude, name);

            WaveformPeaks = await _editorService.GetWaveformPeaksAsync(_pendingClip.FilePath, 300);
            await _playbackService.LoadAsync(_pendingClip);
            _playbackService.Play();

            CanSave = true;
            StatusMessage = "Previewing - tap Save to keep it in your library.";
        }
        finally
        {
            IsBusy = false;
        }
    }

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
            var saved = await _libraryService.AddClipAsync(_pendingClip.FilePath, _pendingClip.Name, isUserRecording: true);
            StatusMessage = saved.PublicStorageLocation is { } location
                ? $"Saved '{saved.Name}' - also in {location}."
                : $"Saved '{saved.Name}' to your library.";

            _pendingClip = null;
            CanSave = false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
