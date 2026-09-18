namespace OvertonesPlayground.Models;

///<summary>
///A small fixed set of <see cref="Track"/>s arranged for mixdown - the "simple layer mixer" data model behind the
///Multi-Track page, Merge, and Mix.
///</summary>
public class MixProject
{
    #region Public properties
    ///<summary>
    ///Unique identifier for the project.
    ///</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    ///<summary>
    ///Display name for the project.
    ///</summary>
    public string Name { get; set; } = string.Empty;

    ///<summary>
    ///Every track in the project.
    ///</summary>
    public List<Track> Tracks { get; init; } = [];
    #endregion
}
