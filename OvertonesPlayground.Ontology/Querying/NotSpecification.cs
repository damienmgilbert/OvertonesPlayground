namespace OvertonesPlayground.Ontology.Querying;

///<summary>
///Holds when the operand does not.
///</summary>
public sealed class NotSpecification : SampleSpecification
{
    #region Fields
    private readonly SampleSpecification _inner;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the negation of a specification.
    ///</summary>
    public NotSpecification(SampleSpecification inner) { _inner = inner; }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public override bool IsSatisfiedBy(Sample sample) => !_inner.IsSatisfiedBy(sample);
    #endregion

    #region Public properties
    ///<inheritdoc/>
    public override string Description => $"NOT {_inner}";
    #endregion
}
