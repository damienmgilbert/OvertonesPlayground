namespace OvertonesPlayground.Ontology.Analysis.Features;

///<summary>
///Measures one category of acoustic facts and returns the matching immutable facet. Implementations are pure and
///deterministic: the same audio always yields the same facet.
///</summary>
///<typeparam name="TFacet">The facet this extractor produces.</typeparam>
public interface IFeatureExtractor<out TFacet>
{
    #region Methods
    ///<summary>Analyses the file held by <paramref name="context"/>.</summary>
    TFacet Extract(AnalysisContext context);
    #endregion
}
