namespace OvertonesPlayground.Ontology.Querying;

///<summary>
///A composable yes / no question about a sample (the specification pattern). Combine with <c>&amp;</c>, <c>|</c> and
public abstract class SampleSpecification
{
    #region Operators
    ///<summary>
    ///The specification must not hold.
    ///</summary>
    public static SampleSpecification operator !(SampleSpecification specification)
    {
        return new NotSpecification(specification);
    }

    ///<summary>
    ///Both specifications must hold.
    ///</summary>
    public static SampleSpecification operator &(SampleSpecification left, SampleSpecification right)
    {
        return new AndSpecification(left, right);
    }

    ///<summary>
    ///Either specification may hold.
    ///</summary>
    public static SampleSpecification operator |(SampleSpecification left, SampleSpecification right)
    {
        return new OrSpecification(left, right);
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Whether <paramref name="sample"/> satisfies this specification.
    ///</summary>
    public abstract bool IsSatisfiedBy(Sample sample);

    ///<inheritdoc/>
    public override string ToString() => Description;
    #endregion

    #region Public properties
    ///<summary>
    ///Readable form, for example <c>(IsA(kick) AND Mono)</c>.
    ///</summary>
    public abstract string Description { get; }
    #endregion
}
