namespace OvertonesPlayground.ViewModels;

///<summary>
///One line of a menu the Launchpad asks the page to show.
///</summary>
///<param name="Text">What the line says.</param>
///<param name="Run">What to do if the user picks it.</param>
public sealed record LaunchpadMenuChoice(string Text, Func<Task> Run);

///<summary>
///A request for the page to show a menu of choices (the Setup and Projects menus), and run the one the user picks.
///</summary>
public sealed class LaunchpadMenuEventArgs(string title, IReadOnlyList<LaunchpadMenuChoice> choices) : EventArgs
{
    #region Public properties
    ///<summary>
    ///The choices, in the order they are listed.
    ///</summary>
    public IReadOnlyList<LaunchpadMenuChoice> Choices { get; } = choices;

    ///<summary>
    ///The menu's title.
    ///</summary>
    public string Title { get; } = title;
    #endregion
}
