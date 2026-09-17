using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

public partial class ToneGeneratorViewModel : BaseViewModel
{
    private readonly ISoundSynthesisService _synthesisService;
    private readonly IAudioEditorService _editorService;
    private readonly IAudioPlaybackService _playbackService;

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
            StatusMessage = $"Saved '{clip.Name}' to your library.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GenerateDrumAsync(DrumType drum)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var clip = await _synthesisService.GenerateDrumAsync(drum, drum.ToString());
            await PreviewAsync(clip);
            StatusMessage = $"Saved '{clip.Name}' to your library.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PreviewAsync(AudioClip clip)
    {
        WaveformPeaks = await _editorService.GetWaveformPeaksAsync(clip.FilePath, 300);
        await _playbackService.LoadAsync(clip);
        _playbackService.Play();
    }
}
