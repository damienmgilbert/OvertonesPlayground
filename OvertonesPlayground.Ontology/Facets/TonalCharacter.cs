namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///Descriptors of the tonality of a sound. Several can apply at once (for example <c>Pitched | Harmonic | Warm</c>).
///</summary>
[Flags]
public enum TonalCharacter
{
    ///<summary>
    ///No descriptor applies.
    ///</summary>
    None = 0,

    ///<summary>
    ///A clear fundamental frequency was detected.
    ///</summary>
    Pitched = 1,

    ///<summary>
    ///No stable fundamental; the sound is percussive or textural.
    ///</summary>
    Unpitched = 2,

    ///<summary>
    ///Energy is spread across the spectrum like noise.
    ///</summary>
    Noisy = 4,

    ///<summary>
    ///Partials sit on integer multiples of the fundamental.
    ///</summary>
    Harmonic = 8,

    ///<summary>
    ///Partials are stretched away from integer multiples (bells, some tuned percussion).
    ///</summary>
    Inharmonic = 16,

    ///<summary>
    ///Bright, ringing, non-harmonic content (cymbals, bells, metallic hits).
    ///</summary>
    Metallic = 32,

    ///<summary>
    ///Most of the energy lives in the sub / bass region.
    ///</summary>
    Sub = 64,

    ///<summary>
    ///Low spectral centroid.
    ///</summary>
    Dark = 128,

    ///<summary>
    ///Mid-low spectral centroid.
    ///</summary>
    Warm = 256,

    ///<summary>
    ///High spectral centroid.
    ///</summary>
    Bright = 512,
}
