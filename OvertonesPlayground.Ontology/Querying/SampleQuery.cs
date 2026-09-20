namespace OvertonesPlayground.Ontology.Querying;

///<summary>
///What to order results by.
///</summary>
public enum SampleSortKey
{
    ///<summary>Alphabetical by name.</summary>
    Name,

    ///<summary>By instrument category, family and name.</summary>
    Instrument,

    ///<summary>By length.</summary>
    Duration,

    ///<summary>By integrated loudness.</summary>
    Loudness,

    ///<summary>By spectral centroid.</summary>
    Brightness,

    ///<summary>By attack time.</summary>
    Attack,

    ///<summary>By effective tempo.</summary>
    Tempo,

    ///<summary>By pitch (key in the name or detected fundamental).</summary>
    Pitch,
}

///<summary>
///A request for samples: free text, a specification, an ordering, and optionally "sounds like this sample".
///</summary>
public sealed record SampleQuery
{
    #region Public properties
    ///<summary>Sort high to low instead of low to high.</summary>
    public bool Descending { get; init; }

    ///<summary>Only samples satisfying this specification are returned.</summary>
    public SampleSpecification? Filter { get; init; }

    ///<summary>Maximum number of results, or null for all.</summary>
    public int? Limit { get; init; }

    ///<summary>Order of the results; ignored when <see cref="SimilarTo"/> is set.</summary>
    public SampleSortKey Sort { get; init; } = SampleSortKey.Name;

    ///<summary>When set, results are ordered by acoustic similarity to this sample (nearest first), excluding itself.</summary>
    public Sample? SimilarTo { get; init; }

    ///<summary>Words that must all appear in the name, instrument, kit, style or key of a result.</summary>
    public string? Text { get; init; }
    #endregion
}
