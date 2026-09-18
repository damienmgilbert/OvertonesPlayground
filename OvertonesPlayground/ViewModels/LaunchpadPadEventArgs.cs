namespace OvertonesPlayground.ViewModels;

///<summary>
///Identifies which pad an event (such as a request to open its menu) is about.
///</summary>
public sealed class LaunchpadPadEventArgs(LaunchpadPadViewModel pad) : EventArgs
{
    #region Public properties
    ///<summary>
    ///The pad the event is about.
    ///</summary>
    public LaunchpadPadViewModel Pad { get; } = pad;
    #endregion
}
