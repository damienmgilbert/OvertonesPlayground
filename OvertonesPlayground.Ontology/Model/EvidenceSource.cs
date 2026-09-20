namespace OvertonesPlayground.Ontology.Model;

///<summary>
///Where a piece of evidence for a label came from.
///</summary>
public enum EvidenceSource
{
    ///<summary>
    ///Words in the file name.
    ///</summary>
    Filename,

    ///<summary>
    ///The measured audio (facets, nearest-neighbour vote).
    ///</summary>
    Signal,

    ///<summary>
    ///Metadata embedded in the file (smpl / acid / iXML chunks).
    ///</summary>
    Metadata,

    ///<summary>
    ///A hand-written override.
    ///</summary>
    Manual,
}
