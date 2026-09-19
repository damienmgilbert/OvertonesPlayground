namespace OvertonesPlayground.Themes;

///<summary>
///Swaps the Fluent light or dark token dictionary in and out of <see cref="Application.Resources"/>, the MAUI equivalent of
///WinUI's ThemeDictionaries. Everything that consumes a token uses <c>{DynamicResource}</c>, so a swap restyles the running
///app; <c>StaticResource</c> would freeze the first theme's colors.
///</summary>
public static class FluentTheme
{
    #region Fields
    private static ResourceDictionary? _current;
    #endregion

    #region Public methods
    ///<summary>
    ///Makes the dictionary for <paramref name="theme"/> the active one. Only the previous theme dictionary is removed: the
    ///metrics and the styles stay merged (clearing <c>MergedDictionaries</c> would delete every implicit style).
    ///</summary>
    public static void Apply(AppTheme theme)
    {
        Application? app = Application.Current;
        if (app is null)
        {
            return;
        }

        // Unspecified means "follow the system", which the platform reports through AppInfo.
        AppTheme effective = theme == AppTheme.Unspecified ? AppInfo.Current.RequestedTheme : theme;
        bool wantsDark = effective == AppTheme.Dark;
        bool isAlreadyActive = _current is not null && (_current is FluentDarkTheme) == wantsDark;
        if (isAlreadyActive)
        {
            return;
        }

        ResourceDictionary next = wantsDark ? new FluentDarkTheme() : new FluentLightTheme();
        ICollection<ResourceDictionary> merged = app.Resources.MergedDictionaries;
        if (_current is not null)
        {
            _ = merged.Remove(_current);
        }

        merged.Add(next);
        _current = next;
    }

    ///<summary>
    ///Reads a color token from the active theme, for code that draws itself (the waveform drawables) and so can't bind to a
    ///resource. Falls back to magenta if the token is missing, which is impossible to miss on screen.
    ///</summary>
    public static Color GetColor(string token) => Application.Current is { } app && app.Resources.TryGetValue(token, out object? value) && value is Color color ? color : Colors.Magenta;
    #endregion
}
