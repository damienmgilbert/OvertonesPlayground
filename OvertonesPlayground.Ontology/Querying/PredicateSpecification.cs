namespace OvertonesPlayground.Ontology.Querying;

///<summary>
///A leaf specification backed by a predicate.
///</summary>
public sealed class PredicateSpecification : SampleSpecification
{
    #region Fields
    private readonly Func<Sample, bool> _predicate;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates a specification from a predicate and a description.
    ///</summary>
    public PredicateSpecification(string description, Func<Sample, bool> predicate)
    {
        Description = description;
        _predicate = predicate;
    }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public override bool IsSatisfiedBy(Sample sample) => _predicate(sample);
    #endregion

    #region Public properties
    ///<inheritdoc/>
    public override string Description { get; }
    #endregion
}
