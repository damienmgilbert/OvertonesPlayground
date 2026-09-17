using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

public partial class SoundCreatorViewModel : BaseViewModel
{
    #region Fields
    [ObservableProperty]
    public partial string ElapsedText { get; set; } = "00:00";

    [ObservableProperty]
    public partial bool IsRecording { get; set; }

    private readonly IAudioLibraryService _libraryService;
    [ObservableProperty]
    public partial string NewClipName { get; set; } = string.Empty;

    private readonly IPermissionsService _permissionsService;
    private readonly IAudioRecorderService _recorderService;
    #endregion

    #region Constructors
    public SoundCreatorViewModel(IAudioRecorderService recorderService, IAudioLibraryService libraryService, IPermissionsService permissionsService)
    {
        _recorderService = recorderService;
        _libraryService = libraryService;
        _permissionsService = permissionsService;
        Title = "Sound Creator";

        _recorderService.ElapsedChanged += (_, elapsed) => ElapsedText = elapsed.ToString(@"mm\:ss");
    }
    #endregion

    #region Private methods
    [RelayCommand]
    private async Task CancelRecordingAsync()
    {
        if (!IsRecording)
        {
            return;
        }

        await _recorderService.CancelAsync();
        IsRecording = false;
        StatusMessage = "Recording discarded.";
    }

    private async Task StartRecordingAsync()
    {
        bool granted = await _permissionsService.EnsureMicrophonePermissionAsync();
        if (!granted)
        {
            StatusMessage = "Microphone permission is required to record.";
            return;
        }

        NewClipName = $"Recording {DateTime.Now:HH:mm:ss}";
        await _recorderService.StartAsync();
        IsRecording = true;
        StatusMessage = null;
    }

    private async Task StopRecordingAsync()
    {
        if (!IsRecording)
        {
            return;
        }

        string name = string.IsNullOrWhiteSpace(NewClipName) ? $"Recording {DateTime.Now:HHmmss}" : NewClipName;
        AudioClip recorded = await _recorderService.StopAsync(name);
        IsRecording = false;

        AudioClip clip = await _libraryService.AddClipAsync(recorded.FilePath, name, isUserRecording: true);
        StatusMessage = clip.PublicStorageLocation is { } location
            ? $"Saved '{clip.Name}' - also in {location}."
            : $"Saved '{clip.Name}' to your library.";
    }

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
}
