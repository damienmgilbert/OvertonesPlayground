using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for the Drum &amp; Bass Synth. "Generate &amp; Play" only previews the sound; it isn't kept until the user
///explicitly taps Save.
///</summary>
public partial class DrumSynthViewModel : BaseViewModel
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
    ///Creates the view model with Kick selected as the initial drum type.
    ///</summary>
    public DrumSynthViewModel(ISoundSynthesisService synthesisService, IAudioEditorService editorService, IAudioPlaybackService playbackService, IAudioLibraryService libraryService, ILogger<DrumSynthViewModel> logger) : base(logger)
    {
        _synthesisService = synthesisService;
        _editorService = editorService;
        _playbackService = playbackService;
        _libraryService = libraryService;
        Title = "Drum Synth";

        SelectedDrumParams = new DrumSynthParametersViewModel(DrumType.Kick);
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Synthesizes the current parameters, previews the result, and enables Save.
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
            Logger.LogDebug("Generating drum sample for {DrumType}.", SelectedDrumParams.DrumType);
            _pendingClip = await _synthesisService.GenerateDrumAsync(SelectedDrumParams.DrumType, SelectedDrumParams.DrumType.ToString(), SelectedDrumParams.ToParameters());

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
            Logger.LogDebug("Saving generated drum sample '{ClipName}'.", _pendingClip.Name);
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
    ///Switches to a new drum type, loading its default parameters and clearing any pending preview.
    ///</summary>
    [RelayCommand]
    private void SelectDrum(DrumType drum)
    {
        Logger.LogDebug("Selected drum type {DrumType}.", drum);
        SelectedDrumParams = new DrumSynthParametersViewModel(drum);
        CanSave = false;
        WaveformPeaks = [];
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Whether there's a generated preview waiting to be saved.
    ///</summary>
    [ObservableProperty]
    public partial bool CanSave { get; set; }

    ///<summary>
    ///Every drum type the Generate row can select from.
    ///</summary>
    public IReadOnlyList<DrumType> DrumOptions { get; } = Enum.GetValues<DrumType>();

    ///<summary>
    ///The tunable parameters for the currently selected drum type.
    ///</summary>
    [ObservableProperty]
    public partial DrumSynthParametersViewModel SelectedDrumParams { get; set; }

    ///<summary>
    ///Waveform peaks for the most recently generated preview, used to draw the waveform view.
    ///</summary>
    [ObservableProperty]
    public partial float[] WaveformPeaks { get; set; } = [];
    #endregion
}
