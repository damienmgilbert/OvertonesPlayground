namespace OvertonesPlayground.Ontology.Model;

///<summary>
///How two samples relate to each other.
///</summary>
public enum SampleRelationType
{
    ///<summary>
    ///Belong to the same kit (for example both are Roland 909).
    ///</summary>
    SameKit,

    ///<summary>
    ///Numbered variations of the same sound.
    ///</summary>
    VariationOf,

    ///<summary>
    ///Complementary drum-kit pieces (kick and snare of the same kit).
    ///</summary>
    Complements,

    ///<summary>
    ///The pitched fundamentals share a pitch class or key.
    ///</summary>
    PitchCompatible,

    ///<summary>
    ///The tempos match (allowing half / double time).
    ///</summary>
    TempoCompatible,

    ///<summary>
    ///Close neighbours in acoustic feature space.
    ///</summary>
    SimilarTo,
}
