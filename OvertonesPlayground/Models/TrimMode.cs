namespace OvertonesPlayground.Models;

///<summary>
///What the Trim page's Save action keeps: the selected range, or everything except it.
///</summary>
public enum TrimMode
{
    ///<summary>
    ///Keeps the [start, end] selection and discards everything outside it.
    ///</summary>
    Trim,

    ///<summary>
    ///Removes the [start, end] selection and splices what's before and after it back together.
    ///</summary>
    TrimMiddle,
}
