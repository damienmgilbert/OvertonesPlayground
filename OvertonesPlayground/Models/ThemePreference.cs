namespace OvertonesPlayground.Models;

///<summary>
///The user's preferred app theme, set on the Settings page.
///</summary>
public enum ThemePreference
{
    ///<summary>
    ///Follow the device's system-wide light/dark setting.
    ///</summary>
    System,

    ///<summary>
    ///Always use the light theme, regardless of the OS setting.
    ///</summary>
    Light,

    ///<summary>
    ///Always use the dark theme, regardless of the OS setting.
    ///</summary>
    Dark,
}
