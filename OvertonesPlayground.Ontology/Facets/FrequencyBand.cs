namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///The seven Hz-defined bands the spectral facet reports energy in. They are defined in hertz (not FFT bins), so files
///recorded at 22.05 kHz and 96 kHz are comparable.
///</summary>
public enum FrequencyBand
{
    ///<summary>
    ///Below 60 Hz.
    ///</summary>
    Sub,

    ///<summary>
    ///60 - 250 Hz.
    ///</summary>
    Bass,

    ///<summary>
    ///250 - 500 Hz.
    ///</summary>
    LowMid,

    ///<summary>
    ///500 Hz - 2 kHz.
    ///</summary>
    Mid,

    ///<summary>
    ///2 - 4 kHz.
    ///</summary>
    HighMid,

    ///<summary>
    ///4 - 8 kHz.
    ///</summary>
    Presence,

    ///<summary>
    ///Above 8 kHz.
    ///</summary>
    Air,
}
