namespace OvertonesPlayground.Models;

///<summary>
///The order the Library page lists its clips in.
///</summary>
public enum LibrarySortOrder
{
    ///<summary>
    ///Most recently added first.
    ///</summary>
    Newest,

    ///<summary>
    ///Least recently added first.
    ///</summary>
    Oldest,

    ///<summary>
    ///Alphabetical by name.
    ///</summary>
    Name,

    ///<summary>
    ///Longest clip first.
    ///</summary>
    Longest,
}
