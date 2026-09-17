using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

/// <summary>
/// View model for generating simple tones and synthesized drums. Exposes options
/// for waveform, frequency, duration and amplitude as well as commands to
/// generate and preview sounds.
/// </summary>
public partial class ToneGeneratorViewModel : BaseViewModel
{
    private readonly ISoundSynthesisService _synthesisService;
    private readonly IAudioEditorService _editorService;
    private readonly IAudioPlaybackService _playbackService;


    /// <summary>Selected waveform type for tone generation.</summary>
    [ObservableProperty]
    public partial WaveformType SelectedWaveform { get; set; } = WaveformType.Sine;

    /// <summary>Frequency in Hz for the generated tone.</summary>
    [ObservableProperty]
    public partial double FrequencyHz { get; set; } = 440;

    /// <summary>Duration of the generated tone in seconds.</summary>
    [ObservableProperty]
    public partial double DurationSeconds { get; set; } = 1.0;

    /// <summary>Amplitude (0.0 - 1.0) for the generated tone.</summary>
    [ObservableProperty]
    public partial double Amplitude { get; set; } = 0.8;

    /// <summary>Preview waveform peaks used by the UI to draw the waveform.</summary>
    [ObservableProperty]
    public partial float[] WaveformPeaks { get; set; } = [];

    /// <summary>Parameters for drum synthesis when generating drum sounds.</summary>
    [ObservableProperty]
    public partial DrumSynthParametersViewModel SelectedDrumParams { get; set; }

    public IReadOnlyList<WaveformType> WaveformOptions { get; } = Enum.GetValues<WaveformType>();

    public IReadOnlyList<DrumType> DrumOptions { get; } = Enum.GetValues<DrumType>();

    public ToneGeneratorViewModel(
        ISoundSynthesisService synthesisService,
        IAudioEditorService editorService,
        IAudioPlaybackService playbackService)
    {
        _synthesisService = synthesisService;
        _editorService = editorService;
        _playbackService = playbackService;
        Title = "Tone Generator";

        SelectedDrumParams = new DrumSynthParametersViewModel(DrumType.Kick);
    }

    [RelayCommand]
    private async Task GenerateToneAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var name = $"{SelectedWaveform} {FrequencyHz:0}Hz";
            var clip = await _synthesisService.GenerateToneAsync(
                SelectedWaveform, FrequencyHz, DurationSeconds, Amplitude, name);

            await PreviewAsync(clip);
            StatusMessage = DescribeSaved(clip);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectDrum(DrumType drum) => SelectedDrumParams = new DrumSynthParametersViewModel(drum);

    [RelayCommand]
    private async Task GenerateSelectedDrumAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var clip = await _synthesisService.GenerateDrumAsync(
                SelectedDrumParams.DrumType, SelectedDrumParams.DrumType.ToString(), SelectedDrumParams.ToParameters());

            await PreviewAsync(clip);
            StatusMessage = DescribeSaved(clip);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string DescribeSaved(AudioClip clip) =>
        clip.PublicStorageLocation is { } location
            ? $"Saved '{clip.Name}' - also in {location}."
            : $"Saved '{clip.Name}' to your library.";

    private async Task PreviewAsync(AudioClip clip)
    {
        WaveformPeaks = await _editorService.GetWaveformPeaksAsync(clip.FilePath, 300);
        await _playbackService.LoadAsync(clip);
        _playbackService.Play();
    }
}
