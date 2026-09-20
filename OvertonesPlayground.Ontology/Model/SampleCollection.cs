namespace OvertonesPlayground.Ontology.Model;

///<summary>
///A named group of samples that belong together. Concrete kinds say <em>why</em> they belong together.
///</summary>
public abstract class SampleCollection
{
    #region Constructors

    ///<summary>
    ///Creates a collection.
    ///</summary>
    protected SampleCollection(string name, IReadOnlyList<Sample> members)
    {
        Name = name;
        Members = members;
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Why the members are grouped, for display.
    ///</summary>
    public abstract string Kind { get; }

    ///<summary>
    ///The samples in the collection.
    ///</summary>
    public IReadOnlyList<Sample> Members { get; }

    ///<summary>
    ///Display name.
    ///</summary>
    public string Name { get; }
    #endregion
}
