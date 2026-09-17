using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for the settings page. Exposes application preferences and commands for global audio control and library
///maintenance.
///</summary>
public partial class SettingsViewModel : BaseViewModel
{
    #region Constants
    private const string KeepScreenOnKey = "keep_screen_on";
    private const string ThemePreferenceKey = "app_theme_preference";
    #endregion

    #region Fields
    private readonly IAudioLibraryService _libraryService;
    private readonly IAudioPlaybackService _playbackService;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the settings view model, restoring the persisted screen-on and theme preferences.
    ///</summary>
    public SettingsViewModel(IAudioLibraryService libraryService, IAudioPlaybackService playbackService, ILogger<SettingsViewModel> logger) : base(logger)
    {
        _libraryService = libraryService;
        _playbackService = playbackService;
        Title = "Settings";

        KeepScreenOn = Preferences.Default.Get(KeepScreenOnKey, false);
        ApplyKeepScreenOn(KeepScreenOn);

        SelectedTheme = LoadSavedThemePreference();
    }
    #endregion

    #region Private methods
    private static void ApplyKeepScreenOn(bool keepOn) { DeviceDisplay.Current.KeepScreenOn = keepOn; }
    ///<summary>
    ///Deletes every clip in the library, including their files on disk.
    ///</summary>
    [RelayCommand]
    private async Task ClearLibraryAsync()
    {
        IReadOnlyList<AudioClip> clips = await _libraryService.GetClipsAsync();
        Logger.LogDebug("Clearing library: {ClipCount} clips.", clips.Count);
        foreach(AudioClip clip in clips)
        {
            await _libraryService.DeleteClipAsync(clip);
        }

        StatusMessage = "Library cleared.";
    }

    partial void OnKeepScreenOnChanged(bool value)
    {
        Logger.LogDebug("Keep screen on changed to {Value}.", value);
        Preferences.Default.Set(KeepScreenOnKey, value);
        ApplyKeepScreenOn(value);
    }

    partial void OnSelectedThemeChanged(ThemePreference value)
    {
        Logger.LogDebug("Theme preference changed to {Theme}.", value);
        Preferences.Default.Set(ThemePreferenceKey, value.ToString());
        ApplyTheme(value);
    }

    ///<summary>
    ///Immediately silences every playing/looping voice across the app.
    ///</summary>
    [RelayCommand]
    private void StopAllAudio()
    {
        Logger.LogDebug("Stopping all audio.");
        _playbackService.StopEverything();
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Applies a theme preference to the running app. Called at startup and whenever the user changes it.
    ///</summary>
    public static void ApplyTheme(ThemePreference preference)
    {
        if(Application.Current is null)
        {
            return;
        }

        Application.Current.UserAppTheme = preference switch
        {
            ThemePreference.Light => AppTheme.Light,
            ThemePreference.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };
    }

    ///<summary>
    ///Reads the persisted theme preference, defaulting to <see cref="ThemePreference.System"/>.
    ///</summary>
    public static ThemePreference LoadSavedThemePreference()
    {
        string saved = Preferences.Default.Get(ThemePreferenceKey, nameof(ThemePreference.System));
        return Enum.TryParse<ThemePreference>(saved, out ThemePreference parsed) ? parsed : ThemePreference.System;
    }
    #endregion

    #region Public properties
    ///<summary>
    ///The installed app's version string, shown at the bottom of the page.
    ///</summary>
    public string AppVersion => AppInfo.Current.VersionString;

    ///<summary>
    ///Whether the device screen should be kept on while the app is running.
    ///</summary>
    [ObservableProperty]
    public partial bool KeepScreenOn { get; set; }

    ///<summary>
    ///The user's preferred app theme (System/Light/Dark), applied immediately when changed.
    ///</summary>
    [ObservableProperty]
    public partial ThemePreference SelectedTheme { get; set; }

    ///<summary>
    ///The choices offered by the theme picker.
    ///</summary>
    public IReadOnlyList<ThemePreference> ThemeOptions { get; } = Enum.GetValues<ThemePreference>();
    #endregion
}
