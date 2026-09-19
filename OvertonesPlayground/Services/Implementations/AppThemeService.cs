using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

///<inheritdoc cref="IThemeService"/>
public class AppThemeService : IThemeService
{
    #region Constants
    private const string ThemePreferenceKey = "app_theme_preference";
    #endregion

    #region Fields
    private readonly IPreferences _preferences;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the theme service.
    ///</summary>
    ///<param name="preferences">Where the chosen theme is kept between runs.</param>
    public AppThemeService(IPreferences preferences)
    {
        _preferences = preferences;
    }
    #endregion

    #region Public methods
    ///<summary>
    ///The <see cref="AppTheme"/> that shows <paramref name="preference"/>; <see cref="AppTheme.Unspecified"/> follows the system.
    ///</summary>
    public static AppTheme ToAppTheme(ThemePreference preference) => preference switch
    {
        ThemePreference.Light => AppTheme.Light,
        ThemePreference.Dark => AppTheme.Dark,
        _ => AppTheme.Unspecified,
    };

    ///<inheritdoc/>
    public void Apply(ThemePreference preference)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.UserAppTheme = ToAppTheme(preference);
    }

    ///<inheritdoc/>
    public ThemePreference LoadSaved()
    {
        string saved = _preferences.Get(ThemePreferenceKey, nameof(ThemePreference.System));
        // TryParse also accepts any number, so a stored "7" has to be rejected explicitly or it would become a theme that doesn't exist.
        return Enum.TryParse<ThemePreference>(saved, out ThemePreference parsed) && Enum.IsDefined(parsed) ? parsed : ThemePreference.System;
    }

    ///<inheritdoc/>
    public void Save(ThemePreference preference) => _preferences.Set(ThemePreferenceKey, preference.ToString());
    #endregion
}
