using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

/// <summary>
/// The two small services that remember settings in <see cref="IPreferences"/>: the theme and the "seen it" flags for one-time tips.
/// </summary>
public sealed class SettingsServicesTests
{
    private readonly FakePreferences _preferences = new();

    #region Theme
    [Theory]
    [InlineData(ThemePreference.System, AppTheme.Unspecified)]
    [InlineData(ThemePreference.Light, AppTheme.Light)]
    [InlineData(ThemePreference.Dark, AppTheme.Dark)]
    public void Theme_EachChoiceMapsToTheAppTheme(ThemePreference preference, AppTheme expected)
    {
        Assert.Equal(expected, AppThemeService.ToAppTheme(preference));
    }

    [Fact]
    public void Theme_NothingSaved_FollowsTheSystem()
    {
        Assert.Equal(ThemePreference.System, new AppThemeService(_preferences).LoadSaved());
    }

    [Theory]
    [InlineData(ThemePreference.System)]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    public void Theme_SavedChoice_IsLoadedBack(ThemePreference choice)
    {
        AppThemeService service = new(_preferences);

        service.Save(choice);

        Assert.Equal(choice, service.LoadSaved());
        Assert.Equal(choice, new AppThemeService(_preferences).LoadSaved());
    }

    [Fact]
    public void Theme_IsSavedByNameUnderTheKeyEarlierVersionsUsed()
    {
        new AppThemeService(_preferences).Save(ThemePreference.Dark);

        Assert.Equal("Dark", _preferences.Get("app_theme_preference", string.Empty));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Sepia")]
    [InlineData("7")]
    [InlineData("-1")]
    public void Theme_SavedValueThatIsNotAChoice_FallsBackToTheSystem(string saved)
    {
        _preferences.Set("app_theme_preference", saved);

        Assert.Equal(ThemePreference.System, new AppThemeService(_preferences).LoadSaved());
    }

    [Fact]
    public void Theme_Apply_BeforeTheAppHasStarted_IsHarmless()
    {
        new AppThemeService(_preferences).Apply(ThemePreference.Dark);
    }
    #endregion

    #region Teaching tips
    [Fact]
    public void Tips_NothingSeenYet()
    {
        Assert.False(new TeachingTipsService(_preferences).HasSeen("library"));
    }

    [Fact]
    public void Tips_MarkedAsSeen_StayAsSeenNextTime()
    {
        TeachingTipsService service = new(_preferences);

        service.MarkSeen("library");

        Assert.True(service.HasSeen("library"));
        Assert.True(new TeachingTipsService(_preferences).HasSeen("library"));
        Assert.False(service.HasSeen("launchpad"));
    }

    [Fact]
    public void Tips_ResetAll_BringsBackEveryTip()
    {
        TeachingTipsService service = new(_preferences);
        service.MarkSeen("library");
        service.MarkSeen("launchpad");
        service.MarkSeen("trim");

        service.ResetAll();

        Assert.False(service.HasSeen("library"));
        Assert.False(service.HasSeen("launchpad"));
        Assert.False(service.HasSeen("trim"));
    }

    [Fact]
    public void Tips_ResetAll_LeavesEveryOtherSettingAlone()
    {
        _preferences.Set("keep_screen_on", true);
        _preferences.Set("app_theme_preference", "Dark");
        TeachingTipsService service = new(_preferences);
        service.MarkSeen("library");

        service.ResetAll();

        Assert.True(_preferences.Get("keep_screen_on", false));
        Assert.Equal("Dark", _preferences.Get("app_theme_preference", string.Empty));
    }

    [Fact]
    public void Tips_ResetAll_ThenSeenAgain_WorksAsNormal()
    {
        TeachingTipsService service = new(_preferences);
        service.MarkSeen("library");
        service.ResetAll();

        service.MarkSeen("library");

        Assert.True(service.HasSeen("library"));
    }

    [Fact]
    public void Tips_MarkedTwice_IsKeptOnceAndStillResets()
    {
        TeachingTipsService service = new(_preferences);

        service.MarkSeen("library");
        service.MarkSeen("library");
        service.ResetAll();

        Assert.False(service.HasSeen("library"));
    }

    [Fact]
    public void Tips_ResetAll_WithNothingSeen_IsHarmless()
    {
        new TeachingTipsService(_preferences).ResetAll();
    }
    #endregion
}
