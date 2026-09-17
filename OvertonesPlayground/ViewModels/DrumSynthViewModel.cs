using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

/// <summary>
/// View model for the Drum &amp; Bass Synth. "Generate &amp; Play" only previews the sound; it
/// isn't kept until the user explicitly taps Save.
/// </summary>
public partial class DrumSynthViewModel : BaseViewModel
{
    private readonly ISoundSynthesisService _synthesisService;
    private readonly IAudioEditorService _editorService;
    private readonly IAudioPlaybackService _playbackService;
    private readonly IAudioLibraryService _libraryService;

    private AudioClip? _pendingClip;

    [ObservableProperty]
    public partial DrumSynthParametersViewModel SelectedDrumParams { get; set; }

    [ObservableProperty]
    public partial float[] WaveformPeaks { get; set; } = [];

    [ObservableProperty]
    public partial bool CanSave { get; set; }

    public IReadOnlyList<DrumType> DrumOptions { get; } = Enum.GetValues<DrumType>();

    public DrumSynthViewModel(
        ISoundSynthesisService synthesisService,
        IAudioEditorService editorService,
        IAudioPlaybackService playbackService,
        IAudioLibraryService libraryService)
    {
        _synthesisService = synthesisService;
        _editorService = editorService;
        _playbackService = playbackService;
        _libraryService = libraryService;
        Title = "Drum Synth";

        SelectedDrumParams = new DrumSynthParametersViewModel(DrumType.Kick);
    }

    [RelayCommand]
    private void SelectDrum(DrumType drum)
    {
        SelectedDrumParams = new DrumSynthParametersViewModel(drum);
        CanSave = false;
        WaveformPeaks = [];
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
            _pendingClip = await _synthesisService.GenerateDrumAsync(
                SelectedDrumParams.DrumType, SelectedDrumParams.DrumType.ToString(), SelectedDrumParams.ToParameters());

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
