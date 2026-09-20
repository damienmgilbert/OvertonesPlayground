namespace OvertonesPlayground.Ontology.Querying;

///<summary>
///Holds when both operands hold.
///</summary>
public sealed class AndSpecification : SampleSpecification
{
    #region Fields
    private readonly SampleSpecification _left;
    private readonly SampleSpecification _right;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the conjunction of two specifications.
    ///</summary>
    public AndSpecification(SampleSpecification left, SampleSpecification right)
    {
        _left = left;
        _right = right;
    }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public override bool IsSatisfiedBy(Sample sample) => _left.IsSatisfiedBy(sample) && _right.IsSatisfiedBy(sample);
    #endregion

    #region Public properties
    ///<inheritdoc/>
    public override string Description => $"({_left} AND {_right})";
    #endregion
}
