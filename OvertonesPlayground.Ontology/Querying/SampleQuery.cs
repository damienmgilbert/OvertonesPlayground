namespace OvertonesPlayground.Ontology.Querying;

///<summary>
///A request for samples: free text, a specification, an ordering, and optionally "sounds like this sample".
///</summary>
public sealed record SampleQuery
{
    #region Public properties

    ///<summary>
    ///Sort high to low instead of low to high.
    ///</summary>
    public bool Descending { get; init; }

    ///<summary>
    ///Only samples satisfying this specification are returned.
    ///</summary>
    public SampleSpecification? Filter { get; init; }

    ///<summary>
    ///Maximum number of results, or null for all.
    ///</summary>
    public int? Limit { get; init; }

    ///<summary>
    ///When set, results are ordered by acoustic similarity to this sample (nearest first), excluding itself.
    ///</summary>
    public Sample? SimilarTo { get; init; }

    ///<summary>
    ///Order of the results; ignored when <see cref="SimilarTo"/> is set.
    ///</summary>
    public SampleSortKey Sort { get; init; } = SampleSortKey.Name;

    ///<summary>
    ///Words that must all appear in the name, instrument, kit, style or key of a result.
    ///</summary>
    public string? Text { get; init; }
    #endregion
}
