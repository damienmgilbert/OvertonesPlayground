namespace OvertonesPlayground.Ontology.Model;

///<summary>
///Numbered variations of one sound (<c>Kick Vinyl 1</c>, <c>Kick Vinyl 2</c> ...), sharing a name stem.
///</summary>
public sealed class VariationSet : SampleCollection
{
    #region Constructors

    ///<summary>
    ///Creates a variation set.
    ///</summary>
    public VariationSet(string stem, IReadOnlyList<Sample> members) : base(stem, members)
    {
    }
    #endregion

    #region Public properties
    ///<inheritdoc/>
    public override string Kind => "Variations";
    #endregion
}
