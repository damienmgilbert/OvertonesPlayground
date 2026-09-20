namespace OvertonesPlayground.Ontology.Model;

///<summary>
///Result of analysing a file.
///</summary>
public enum AnalysisStatus
{
    ///<summary>
    ///Every facet was extracted.
    ///</summary>
    Analyzed,

    ///<summary>
    ///The audio was readable but at least one analyzer failed or was skipped.
    ///</summary>
    Partial,

    ///<summary>
    ///The container is not supported (for example AIFF-C); only the filename labels exist.
    ///</summary>
    UnsupportedFormat,

    ///<summary>
    ///The file could not be read.
    ///</summary>
    Failed,
}
