namespace OvertonesPlayground.Models;

///<summary>
///Where a Launchpad project came from.
///</summary>
public enum LaunchpadProjectOrigin
{
    ///<summary>Made on the pads by the user (or an old save, which records no origin).</summary>
    User,

    ///<summary>A ready-made example or style preset.</summary>
    Example,

    ///<summary>Made by the project generator.</summary>
    Generated,
}
