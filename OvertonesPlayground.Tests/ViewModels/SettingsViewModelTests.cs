namespace OvertonesPlayground.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    private readonly IAppInfo _appInfo = Substitute.For<IAppInfo>();
    private readonly IDeviceDisplay _deviceDisplay = Substitute.For<IDeviceDisplay>();
    private readonly IAudioLibraryService _library = Substitute.For<IAudioLibraryService>();
    private readonly IAudioPlaybackService _playback = Substitute.For<IAudioPlaybackService>();
    private readonly FakePreferences _preferences = new();
    private readonly ITeachingTipsService _tips = Substitute.For<ITeachingTipsService>();
    private readonly IThemeService _theme = Substitute.For<IThemeService>();

    private SettingsViewModel Create() => new(_library, _playback, _preferences, _deviceDisplay, _appInfo, _theme, _tips, NullLogger<SettingsViewModel>.Instance);

    #region Construction
    [Fact]
    public void Constructor_NothingSaved_LeavesTheScreenAndThemeAtTheirDefaults()
    {
        SettingsViewModel viewModel = Create();

        Assert.False(viewModel.KeepScreenOn);
        Assert.False(_deviceDisplay.KeepScreenOn);
        Assert.Equal(ThemePreference.System, viewModel.SelectedTheme);
        Assert.Equal("Settings", viewModel.Title);
    }

    [Fact]
    public void Constructor_KeepScreenOnWasSaved_RestoresItAndAppliesItToTheDevice()
    {
        _preferences.Set("keep_screen_on", true);

        SettingsViewModel viewModel = Create();

        Assert.True(viewModel.KeepScreenOn);
        Assert.True(_deviceDisplay.KeepScreenOn);
    }

    [Fact]
    public void Constructor_ThemeWasSaved_ShowsTheSavedTheme()
    {
        _theme.LoadSaved().Returns(ThemePreference.Dark);

        SettingsViewModel viewModel = Create();

        Assert.Equal(ThemePreference.Dark, viewModel.SelectedTheme);
    }

    [Fact]
    public void ThemeOptions_OffersEveryTheme()
    {
        SettingsViewModel viewModel = Create();

        Assert.Equal([ThemePreference.System, ThemePreference.Light, ThemePreference.Dark], viewModel.ThemeOptions);
    }

    [Fact]
    public void AppVersion_ComesFromTheInstalledApp()
    {
        _appInfo.VersionString.Returns("3.4.5");

        Assert.Equal("3.4.5", Create().AppVersion);
    }
    #endregion

    #region Changing settings
    [Fact]
    public void KeepScreenOn_Changed_IsSavedAndAppliedToTheDevice()
    {
        SettingsViewModel viewModel = Create();

        viewModel.KeepScreenOn = true;

        Assert.True(_preferences.Get("keep_screen_on", false));
        Assert.True(_deviceDisplay.KeepScreenOn);

        viewModel.KeepScreenOn = false;

        Assert.False(_preferences.Get("keep_screen_on", true));
        Assert.False(_deviceDisplay.KeepScreenOn);
    }

    [Theory]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    public void SelectedTheme_Changed_IsSavedAndAppliedToTheApp(ThemePreference choice)
    {
        SettingsViewModel viewModel = Create();
        _theme.ClearReceivedCalls();

        viewModel.SelectedTheme = choice;

        Received.InOrder(() =>
        {
            _theme.Save(choice);
            _theme.Apply(choice);
        });
    }

    [Fact]
    public void SelectedTheme_SetToWhatItAlreadyIs_DoesNothing()
    {
        SettingsViewModel viewModel = Create();
        _theme.ClearReceivedCalls();

        viewModel.SelectedTheme = ThemePreference.System;

        _theme.DidNotReceiveWithAnyArgs().Save(default);
        _theme.DidNotReceiveWithAnyArgs().Apply(default);
    }
    #endregion

    #region Commands
    [Fact]
    public void ShowTipsAgain_ForgetsEveryTipAndSaysSo()
    {
        SettingsViewModel viewModel = Create();

        viewModel.ShowTipsAgainCommand.Execute(null);

        _tips.Received(1).ResetAll();
        Assert.StartsWith("Tips will show again", viewModel.StatusMessage);
    }

    [Fact]
    public void StopAllAudio_SilencesEverything()
    {
        SettingsViewModel viewModel = Create();

        viewModel.StopAllAudioCommand.Execute(null);

        _playback.Received(1).StopEverything();
    }

    [Fact]
    public async Task ClearLibrary_DeletesEveryClipAndSaysSo()
    {
        AudioClip first = TestData.Clip("First");
        AudioClip second = TestData.Clip("Second");
        _library.GetClipsAsync().Returns(TestData.Clips(first, second));
        SettingsViewModel viewModel = Create();

        await viewModel.ClearLibraryCommand.ExecuteAsync(null);

        await _library.Received(1).DeleteClipAsync(first);
        await _library.Received(1).DeleteClipAsync(second);
        Assert.Equal("Library cleared.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ClearLibrary_LibraryAlreadyEmpty_DeletesNothingButStillReports()
    {
        _library.GetClipsAsync().Returns(TestData.Clips());
        SettingsViewModel viewModel = Create();

        await viewModel.ClearLibraryCommand.ExecuteAsync(null);

        await _library.DidNotReceiveWithAnyArgs().DeleteClipAsync(default!);
        Assert.Equal("Library cleared.", viewModel.StatusMessage);
    }
    #endregion
}
