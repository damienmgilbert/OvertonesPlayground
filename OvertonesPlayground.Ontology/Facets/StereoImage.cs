namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///How a sample uses the stereo field.
///</summary>
public enum StereoImage
{
    ///<summary>
    ///A single channel.
    ///</summary>
    Mono,

    ///<summary>
    ///Two channels carrying identical audio (a mono sound stored as stereo).
    ///</summary>
    DualMono,

    ///<summary>
    ///Two channels that are strongly correlated (a slightly widened mono image).
    ///</summary>
    Narrow,

    ///<summary>
    ///Two decorrelated channels with a real stereo image.
    ///</summary>
    Wide,

    ///<summary>
    ///Channels are largely polarity-inverted; summing to mono cancels much of the sound.
    ///</summary>
    OutOfPhase,
}
