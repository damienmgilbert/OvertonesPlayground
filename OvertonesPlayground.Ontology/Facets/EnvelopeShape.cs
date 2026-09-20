namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///Overall amplitude-envelope archetype of a sample.
///</summary>
public enum EnvelopeShape
{
    ///<summary>
    ///Very fast attack and a short decay (kicks, snares, closed hats, clicks).
    ///</summary>
    Impulsive,

    ///<summary>
    ///Fast attack followed by a longer natural decay (plucks, open hats, cymbals, bells).
    ///</summary>
    Plucked,

    ///<summary>
    ///Holds its level for most of its length and fades out (pads, organs, held notes).
    ///</summary>
    Sustained,

    ///<summary>
    ///Level builds up slowly to a peak away from the start (risers, swells).
    ///</summary>
    Swell,

    ///<summary>
    ///Builds to a peak right at the end and stops (reversed sounds).
    ///</summary>
    Reverse,

    ///<summary>
    ///Sustained, then cut off abruptly (gated sounds, gated reverb).
    ///</summary>
    Gated,
}
