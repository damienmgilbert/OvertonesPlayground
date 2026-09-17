using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

/// <summary>
/// View model for the settings page. Exposes application preferences and
/// commands for global audio control and library maintenance.
/// </summary>
public partial class SettingsViewModel : BaseViewModel
{
    private const string KeepScreenOnKey = "keep_screen_on";
    private const string ThemePreferenceKey = "app_theme_preference";

    private readonly IAudioLibraryService _libraryService;
    private readonly IAudioPlaybackService _playbackService;

    /// <summary>Whether the device screen should be kept on while the app is running.</summary>
    [ObservableProperty]
    public partial bool KeepScreenOn { get; set; }

    /// <summary>The user's preferred app theme (System/Light/Dark), applied immediately when changed.</summary>
    [ObservableProperty]
    public partial ThemePreference SelectedTheme { get; set; }

    /// <summary>The choices offered by the theme picker.</summary>
    public IReadOnlyList<ThemePreference> ThemeOptions { get; } = Enum.GetValues<ThemePreference>();

    /// <summary>The installed app's version string, shown at the bottom of the page.</summary>
    public string AppVersion => AppInfo.Current.VersionString;

    /// <summary>Creates the settings view model, restoring the persisted screen-on and theme preferences.</summary>
    public SettingsViewModel(IAudioLibraryService libraryService, IAudioPlaybackService playbackService)
    {
        _libraryService = libraryService;
        _playbackService = playbackService;
        Title = "Settings";

        KeepScreenOn = Preferences.Default.Get(KeepScreenOnKey, false);
        ApplyKeepScreenOn(KeepScreenOn);

        SelectedTheme = LoadSavedThemePreference();
    }

    partial void OnKeepScreenOnChanged(bool value)
    {
        Preferences.Default.Set(KeepScreenOnKey, value);
        ApplyKeepScreenOn(value);
    }

    partial void OnSelectedThemeChanged(ThemePreference value)
    {
        Preferences.Default.Set(ThemePreferenceKey, value.ToString());
        ApplyTheme(value);
    }

    /// <summary>Reads the persisted theme preference, defaulting to <see cref="ThemePreference.System"/>.</summary>
    public static ThemePreference LoadSavedThemePreference()
    {
        var saved = Preferences.Default.Get(ThemePreferenceKey, nameof(ThemePreference.System));
        return Enum.TryParse<ThemePreference>(saved, out var parsed) ? parsed : ThemePreference.System;
    }

    /// <summary>Applies a theme preference to the running app. Called at startup and whenever the user changes it.</summary>
    public static void ApplyTheme(ThemePreference preference)
    {
        if (Application.Current is null)
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

    private static void ApplyKeepScreenOn(bool keepOn) =>
        DeviceDisplay.Current.KeepScreenOn = keepOn;

    /// <summary>Immediately silences every playing/looping voice across the app.</summary>
    [RelayCommand]
    private void StopAllAudio() => _playbackService.StopEverything();

    /// <summary>Deletes every clip in the library, including their files on disk.</summary>
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
