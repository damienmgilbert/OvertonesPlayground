namespace OvertonesPlayground.Ontology.Querying;

///<summary>
///Holds when either operand holds.
///</summary>
public sealed class OrSpecification : SampleSpecification
{
    #region Fields
    private readonly SampleSpecification _left;
    private readonly SampleSpecification _right;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the disjunction of two specifications.
    ///</summary>
    public OrSpecification(SampleSpecification left, SampleSpecification right)
    {
        _left = left;
        _right = right;
    }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public override bool IsSatisfiedBy(Sample sample) => _left.IsSatisfiedBy(sample) || _right.IsSatisfiedBy(sample);
    #endregion

    #region Public properties
    ///<inheritdoc/>
    public override string Description => $"({_left} OR {_right})";
    #endregion
}
