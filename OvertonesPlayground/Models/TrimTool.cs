namespace OvertonesPlayground.Models;

///<summary>
///Which quick-action slider, if any, the Trim page's bottom bar currently has revealed.
///</summary>
public enum TrimTool
{
    ///<summary>
    ///No quick-action slider is revealed.
    ///</summary>
    None,

    ///<summary>
    ///The volume (gain) slider is revealed.
    ///</summary>
    Volume,

    ///<summary>
    ///The fade-in duration slider is revealed.
    ///</summary>
    FadeIn,

    ///<summary>
    ///The fade-out duration slider is revealed.
    ///</summary>
    FadeOut,
}
