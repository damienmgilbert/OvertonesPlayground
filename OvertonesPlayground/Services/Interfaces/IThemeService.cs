using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Remembers the user's light/dark/system theme choice and applies it to the running app.
///</summary>
public interface IThemeService
{
    #region Methods
    ///<summary>
    ///Applies <paramref name="preference"/> to the running app. Does nothing before the app has started.
    ///</summary>
    void Apply(ThemePreference preference);

    ///<summary>
    ///Reads the saved preference, defaulting to <see cref="ThemePreference.System"/> when none is saved or the saved value is
    ///not a valid choice.
    ///</summary>
    ThemePreference LoadSaved();

    ///<summary>
    ///Saves <paramref name="preference"/> so it is restored the next time the app starts.
    ///</summary>
    void Save(ThemePreference preference);
    #endregion
}
