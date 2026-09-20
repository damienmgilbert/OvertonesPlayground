namespace OvertonesPlayground.Ontology.Querying;

///<summary>
///A composable yes / no question about a sample (the specification pattern). Combine with <c>&amp;</c>, <c>|</c> and
///<c>!</c>: <c>SampleSpecs.InstrumentIs("kick") &amp; SampleSpecs.Mono() &amp; !SampleSpecs.Length(LengthClass.Long)</c>.
///</summary>
public abstract class SampleSpecification
{
    #region Operators
    ///<summary>Both specifications must hold.</summary>
    public static SampleSpecification operator &(SampleSpecification left, SampleSpecification right) => new AndSpecification(left, right);

    ///<summary>Either specification may hold.</summary>
    public static SampleSpecification operator |(SampleSpecification left, SampleSpecification right) => new OrSpecification(left, right);

    ///<summary>The specification must not hold.</summary>
    public static SampleSpecification operator !(SampleSpecification specification) => new NotSpecification(specification);
    #endregion

    #region Public methods
    ///<summary>Whether <paramref name="sample"/> satisfies this specification.</summary>
    public abstract bool IsSatisfiedBy(Sample sample);

    ///<inheritdoc/>
    public override string ToString() => Description;
    #endregion

    #region Public properties
    ///<summary>Readable form, for example <c>(IsA(kick) AND Mono)</c>.</summary>
    public abstract string Description { get; }
    #endregion
}

///<summary>
///A leaf specification backed by a predicate.
///</summary>
public sealed class PredicateSpecification : SampleSpecification
{
    #region Fields
    private readonly Func<Sample, bool> _predicate;
    #endregion

    #region Constructors
    ///<summary>Creates a specification from a predicate and a description.</summary>
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
    ///<summary>Creates the conjunction of two specifications.</summary>
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
    ///<summary>Creates the disjunction of two specifications.</summary>
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

///<summary>
///Holds when the operand does not.
///</summary>
public sealed class NotSpecification : SampleSpecification
{
    #region Fields
    private readonly SampleSpecification _inner;
    #endregion

    #region Constructors
    ///<summary>Creates the negation of a specification.</summary>
    public NotSpecification(SampleSpecification inner)
    {
        _inner = inner;
    }
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
