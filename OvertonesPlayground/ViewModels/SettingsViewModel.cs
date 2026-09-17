using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

/// <summary>
/// View model for the settings page. Exposes application preferences and
/// commands for global audio control and library maintenance.
/// </summary>
public partial class SettingsViewModel : BaseViewModel
{
    private const string KeepScreenOnKey = "keep_screen_on";

    private readonly IAudioLibraryService _libraryService;
    private readonly IAudioPlaybackService _playbackService;

    /// <summary>Whether the device screen should be kept on while the app is running.</summary>
    [ObservableProperty]
    public partial bool KeepScreenOn { get; set; }

    public string AppVersion => AppInfo.Current.VersionString;

    public SettingsViewModel(IAudioLibraryService libraryService, IAudioPlaybackService playbackService)
    {
        _libraryService = libraryService;
        _playbackService = playbackService;
        Title = "Settings";
        KeepScreenOn = Preferences.Default.Get(KeepScreenOnKey, false);
        ApplyKeepScreenOn(KeepScreenOn);
    }

    partial void OnKeepScreenOnChanged(bool value)
    {
        Preferences.Default.Set(KeepScreenOnKey, value);
        ApplyKeepScreenOn(value);
    }

    private static void ApplyKeepScreenOn(bool keepOn) =>
        DeviceDisplay.Current.KeepScreenOn = keepOn;

    [RelayCommand]
    private void StopAllAudio() => _playbackService.StopEverything();

    [RelayCommand]
    private async Task ClearLibraryAsync()
    {
        var clips = await _libraryService.GetClipsAsync();
        foreach (var clip in clips)
        {
            await _libraryService.DeleteClipAsync(clip);
        }

        StatusMessage = "Library cleared.";
    }
}
