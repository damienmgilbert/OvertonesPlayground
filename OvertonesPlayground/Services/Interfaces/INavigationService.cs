namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Moves between the app's pages by Shell route. View models go through this instead of <c>Shell.Current</c>, so the routes they
///navigate to can be checked without a running Shell.
///</summary>
public interface INavigationService
{
    #region Methods
    ///<summary>
    ///Navigates to <paramref name="route"/>: a route with an optional query string (<c>"editor?clipId=abc"</c>), an absolute
    ///route (<c>"//player"</c>), or <c>".."</c> to go back one page.
    ///</summary>
    Task GoToAsync(string route);
    #endregion
}
