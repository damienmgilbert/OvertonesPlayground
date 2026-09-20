namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///How loud a sample is, in coarse buckets of integrated loudness (corpus quartiles).
///</summary>
public enum LoudnessClass
{
    ///<summary>
    ///Integrated loudness below -18 LUFS.
    ///</summary>
    Quiet,

    ///<summary>
    ///-18 to -14.5 LUFS.
    ///</summary>
    Moderate,

    ///<summary>
    ///-14.5 to -11.5 LUFS.
    ///</summary>
    Loud,

    ///<summary>
    ///Above -11.5 LUFS.
    ///</summary>
    VeryLoud,
}
