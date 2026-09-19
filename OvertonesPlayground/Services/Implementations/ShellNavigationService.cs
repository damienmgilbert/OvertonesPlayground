using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

///<summary>
///Navigates through the app's <see cref="Shell"/>. This is the device boundary for navigation: it needs a real Shell, so it is
///checked on a device rather than in the unit tests, which cover the routes the view models ask for.
///</summary>
public class ShellNavigationService : INavigationService
{
    #region Public methods
    ///<inheritdoc/>
    public Task GoToAsync(string route)
    {
        Shell shell = Shell.Current ?? throw new InvalidOperationException("Can't navigate before the app's Shell is showing.");
        return shell.GoToAsync(route);
    }
    #endregion
}
