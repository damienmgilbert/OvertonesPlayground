namespace OvertonesPlayground.Models;

///<summary>
///Types of simple filters that can be applied to synthesized sounds.
///</summary>
public enum FilterType
{
    ///<summary>
    ///No filtering applied.
    ///</summary>
    None,

    ///<summary>
    ///Low-pass filter allowing frequencies below the cutoff.
    ///</summary>
    LowPass,

    ///<summary>
    ///High-pass filter allowing frequencies above the cutoff.
    ///</summary>
    HighPass,

    ///<summary>
    ///Band-pass filter centered around the cutoff frequency.
    ///</summary>
    BandPass,
}
