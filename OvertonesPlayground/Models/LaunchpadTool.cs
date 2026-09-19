namespace OvertonesPlayground.Models;

///<summary>
///An edit tool armed from the left-hand buttons; the next pad (or step, or pattern) tapped is the one it acts on.
///</summary>
public enum LaunchpadTool
{
    ///<summary>
    ///No tool armed.
    ///</summary>
    None,

    ///<summary>
    ///Clears whatever is tapped.
    ///</summary>
    Clear,

    ///<summary>
    ///Copies the first thing tapped onto the second.
    ///</summary>
    Duplicate,
}
