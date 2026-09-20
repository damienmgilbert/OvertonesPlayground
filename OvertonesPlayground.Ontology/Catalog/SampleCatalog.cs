namespace OvertonesPlayground.Ontology.Catalog;

///<summary>
///The precomputed description of every bundled sample. Produced offline by the SampleAnalyzer tool and shipped as the
///asset <c>sample-catalog.json</c>; the app only ever reads it.
///</summary>
///<param name="SchemaVersion">Bumped when the JSON shape changes incompatibly.</param>
///<param name="AnalyzerVersion">Bumped when analysis or classification logic changes, so stale catalogs can be detected.</param>
///<param name="Samples">One entry per audio file, ordered by file name.</param>
public sealed record SampleCatalog(int SchemaVersion, string AnalyzerVersion, IReadOnlyList<Sample> Samples)
{
    #region Constants
    ///<summary>The schema version this build reads and writes.</summary>
    public const int CurrentSchemaVersion = 1;

    ///<summary>Version of the analysis and classification logic.</summary>
    public const string CurrentAnalyzerVersion = "1.0.0";
    #endregion

    #region Public methods
    ///<summary>Builds a catalog of the current versions from <paramref name="samples"/>, ordered by file name.</summary>
    public static SampleCatalog Create(IEnumerable<Sample> samples) =>
        new(CurrentSchemaVersion, CurrentAnalyzerVersion, [.. samples.OrderBy(sample => sample.Id, StringComparer.OrdinalIgnoreCase)]);
    #endregion
}
