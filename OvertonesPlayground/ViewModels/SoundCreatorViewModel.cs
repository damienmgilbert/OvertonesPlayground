using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model used by the sound recording/creation page. Manages recording state, elapsed time display and interactions
///with the audio recorder and library.
///</summary>
public partial class SoundCreatorViewModel : BaseViewModel
{
    #region Fields
    private readonly IAudioLibraryService _libraryService;
    private readonly IPermissionsService _permissionsService;
    private readonly IAudioRecorderService _recorderService;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the view model and subscribes to the recorder's elapsed-time updates.
    ///</summary>
    public SoundCreatorViewModel(IAudioRecorderService recorderService, IAudioLibraryService libraryService, IPermissionsService permissionsService, ILogger<SoundCreatorViewModel> logger) : base(logger)
    {
        _recorderService = recorderService;
        _libraryService = libraryService;
        _permissionsService = permissionsService;
        Title = "Audio Recorder";

        _recorderService.ElapsedChanged += (_, elapsed) => ElapsedText = elapsed.ToString(@"mm\:ss");
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Stops recording, if in progress, and discards the captured audio.
    ///</summary>
    [RelayCommand]
    private async Task CancelRecordingAsync()
    {
        if (!IsRecording)
        {
            return;
        }

        Log_CancelingRecording();
        await _recorderService.CancelAsync();
        IsRecording = false;
        StatusMessage = "Recording discarded.";
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Canceling recording.")]
    private partial void Log_CancelingRecording();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Microphone permission denied.")]
    private partial void Log_MicrophonePermissionDenied();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting recording '{ClipName}'.")]
    private partial void Log_StartingRecording(string clipName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Stopping recording, saving as '{ClipName}'.")]
    private partial void Log_StoppingRecording(string clipName);

    ///<summary>
    ///Requests microphone permission if needed, then starts recording.
    ///</summary>
    private async Task StartRecordingAsync()
    {
        bool granted = await _permissionsService.EnsureMicrophonePermissionAsync();
        if (!granted)
        {
            Log_MicrophonePermissionDenied();
            StatusMessage = "Microphone permission is required to record.";
            return;
        }

        NewClipName = $"Recording {DateTime.Now:HH:mm:ss}";
        Log_StartingRecording(NewClipName);
        await _recorderService.StartAsync();
        IsRecording = true;
        StatusMessage = null;
    }

    ///<summary>
    ///Stops recording and saves the captured audio into the library.
    ///</summary>
    private async Task StopRecordingAsync()
    {
        if (!IsRecording)
        {
            return;
        }

        string name = string.IsNullOrWhiteSpace(NewClipName) ? $"Recording {DateTime.Now:HHmmss}" : NewClipName;
        Log_StoppingRecording(name);
        AudioClip recorded = await _recorderService.StopAsync(name);
        IsRecording = false;

        AudioClip clip = await _libraryService.AddClipAsync(recorded.FilePath, name, isUserRecording: true);
        StatusMessage = clip.PublicStorageLocation is { } location ? $"Saved '{clip.Name}' - also in {location}." : $"Saved '{clip.Name}' to your library.";
    }

    ///<summary>
    ///Starts recording if idle, or stops (and saves) it if already recording.
    ///</summary>
    [RelayCommand]
    private async Task ToggleRecordingAsync()
    {
        if (IsRecording)
        {
            await StopRecordingAsync();
        }
        else
        {
            await StartRecordingAsync();
        }
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Human readable elapsed time for the current recording.
    ///</summary>
    [ObservableProperty]
    public partial string ElapsedText { get; set; } = "00:00";

    ///<summary>
    ///Whether a recording is currently in progress.
    ///</summary>
    [ObservableProperty]
    public partial bool IsRecording { get; set; }

    ///<summary>
    ///Name proposed for the newly recorded clip.
    ///</summary>
    [ObservableProperty]
    public partial string NewClipName { get; set; } = string.Empty;
    #endregion
}
