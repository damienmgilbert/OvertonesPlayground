namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///Descriptors of the tonality of a sound. Several can apply at once (for example <c>Pitched | Harmonic | Warm</c>).
///</summary>
[Flags]
public enum TonalCharacter
{
    ///<summary>No descriptor applies.</summary>
    None = 0,

    ///<summary>A clear fundamental frequency was detected.</summary>
    Pitched = 1,

    ///<summary>No stable fundamental; the sound is percussive or textural.</summary>
    Unpitched = 2,

    ///<summary>Energy is spread across the spectrum like noise.</summary>
    Noisy = 4,

    ///<summary>Partials sit on integer multiples of the fundamental.</summary>
    Harmonic = 8,

    ///<summary>Partials are stretched away from integer multiples (bells, some tuned percussion).</summary>
    Inharmonic = 16,

    ///<summary>Bright, ringing, non-harmonic content (cymbals, bells, metallic hits).</summary>
    Metallic = 32,

    ///<summary>Most of the energy lives in the sub / bass region.</summary>
    Sub = 64,

    ///<summary>Low spectral centroid.</summary>
    Dark = 128,

    ///<summary>Mid-low spectral centroid.</summary>
    Warm = 256,

    ///<summary>High spectral centroid.</summary>
    Bright = 512,
}

///<summary>
///Overall amplitude-envelope archetype of a sample.
///</summary>
public enum EnvelopeShape
{
    ///<summary>Very fast attack and a short decay (kicks, snares, closed hats, clicks).</summary>
    Impulsive,

    ///<summary>Fast attack followed by a longer natural decay (plucks, open hats, cymbals, bells).</summary>
    Plucked,

    ///<summary>Holds its level for most of its length and fades out (pads, organs, held notes).</summary>
    Sustained,

    ///<summary>Level builds up slowly to a peak away from the start (risers, swells).</summary>
    Swell,

    ///<summary>Builds to a peak right at the end and stops (reversed sounds).</summary>
    Reverse,

    ///<summary>Sustained, then cut off abruptly (gated sounds, gated reverb).</summary>
    Gated,
}

///<summary>
///How a sample uses the stereo field.
///</summary>
public enum StereoImage
{
    ///<summary>A single channel.</summary>
    Mono,

    ///<summary>Two channels carrying identical audio (a mono sound stored as stereo).</summary>
    DualMono,

    ///<summary>Two channels that are strongly correlated (a slightly widened mono image).</summary>
    Narrow,

    ///<summary>Two decorrelated channels with a real stereo image.</summary>
    Wide,

    ///<summary>Channels are largely polarity-inverted; summing to mono cancels much of the sound.</summary>
    OutOfPhase,
}

///<summary>
///Coarse length buckets used for browsing.
///</summary>
public enum LengthClass
{
    ///<summary>Under a quarter of a second.</summary>
    Hit,

    ///<summary>Under one second.</summary>
    Short,

    ///<summary>One to four seconds.</summary>
    Medium,

    ///<summary>Four to fifteen seconds.</summary>
    Long,

    ///<summary>Fifteen seconds or more.</summary>
    Extended,
}

///<summary>
///How loud a sample is, in coarse buckets of integrated loudness (corpus quartiles).
///</summary>
public enum LoudnessClass
{
    ///<summary>Integrated loudness below -18 LUFS.</summary>
    Quiet,

    ///<summary>-18 to -14.5 LUFS.</summary>
    Moderate,

    ///<summary>-14.5 to -11.5 LUFS.</summary>
    Loud,

    ///<summary>Above -11.5 LUFS.</summary>
    VeryLoud,
}
