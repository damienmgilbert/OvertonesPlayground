using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    #endregion

    #region Fields
    private readonly IAppInfo _appInfo;
    private readonly IDeviceDisplay _deviceDisplay;
    private readonly IAudioLibraryService _libraryService;
    private readonly IAudioPlaybackService _playbackService;
    private readonly IPreferences _preferences;
    private readonly ITeachingTipsService _teachingTips;
    private readonly IThemeService _themeService;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the settings view model, restoring the persisted screen-on and theme preferences.
    ///</summary>
    public SettingsViewModel(IAudioLibraryService libraryService, IAudioPlaybackService playbackService, IPreferences preferences, IDeviceDisplay deviceDisplay, IAppInfo appInfo, IThemeService themeService, ITeachingTipsService teachingTips, ILogger<SettingsViewModel> logger) : base(logger)
    {
        _libraryService = libraryService;
        _playbackService = playbackService;
        _preferences = preferences;
        _deviceDisplay = deviceDisplay;
        _appInfo = appInfo;
        _themeService = themeService;
        _teachingTips = teachingTips;
        Title = "Settings";

        KeepScreenOn = _preferences.Get(KeepScreenOnKey, false);
        ApplyKeepScreenOn(KeepScreenOn);

        SelectedTheme = _themeService.LoadSaved();
    }
    #endregion

    #region Private methods
    private void ApplyKeepScreenOn(bool keepOn) => _deviceDisplay.KeepScreenOn = keepOn;

    ///<summary>
    ///Deletes every clip in the library, including their files on disk.
    ///</summary>
    [RelayCommand]
    private async Task ClearLibraryAsync()
    {
        IReadOnlyList<AudioClip> clips = await _libraryService.GetClipsAsync();
        Log_ClearingLibrary(clips.Count);
        foreach (AudioClip clip in clips)
        {
            await _libraryService.DeleteClipAsync(clip);
        }

        StatusMessage = "Library cleared.";
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Clearing library: {ClipCount} clips.")]
    private partial void Log_ClearingLibrary(int clipCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Keep screen on changed to {Value}.")]
    private partial void Log_KeepScreenOnChanged(bool value);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Stopping all audio.")]
    private partial void Log_StoppingAllAudio();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Theme preference changed to {Theme}.")]
    private partial void Log_ThemeChanged(ThemePreference theme);

    partial void OnKeepScreenOnChanged(bool value)
    {
        Log_KeepScreenOnChanged(value);
        _preferences.Set(KeepScreenOnKey, value);
        ApplyKeepScreenOn(value);
    }

    partial void OnSelectedThemeChanged(ThemePreference value)
    {
        Log_ThemeChanged(value);
        _themeService.Save(value);
        _themeService.Apply(value);
    }

    ///<summary>
    ///Immediately silences every playing/looping voice across the app.
    ///</summary>
    ///<summary>
    ///Brings back the one-time tips (the <c>TeachingPopover</c> on the Library, Launchpad and Trim pages), so each shows
    ///again the next time its page opens.
    ///</summary>
    [RelayCommand]
    private void ShowTipsAgain()
    {
        _teachingTips.ResetAll();
        StatusMessage = "Tips will show again the next time you open the Library, Launchpad and Trim pages.";
    }

    [RelayCommand]
    private void StopAllAudio()
    {
        Log_StoppingAllAudio();
        _playbackService.StopEverything();
    }
    #endregion

    #region Public properties
    ///<summary>
    ///The installed app's version string, shown at the bottom of the page.
    ///</summary>
    public string AppVersion => _appInfo.VersionString;

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
