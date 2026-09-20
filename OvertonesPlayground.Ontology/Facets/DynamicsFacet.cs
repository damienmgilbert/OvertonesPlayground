using System.Text.Json.Serialization;

namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///Amplitude and loudness: levels, loudness, dynamics and the amplitude envelope.
///</summary>
///<param name="PeakDb">Sample peak in dBFS.</param>
///<param name="TruePeakDb">Inter-sample (4x oversampled) peak in dBFS.</param>
///<param name="RmsDb">RMS over the whole file in dBFS.</param>
///<param name="ActiveRmsDb">RMS over the active region (where the sound is audible) in dBFS.</param>
///<param name="IntegratedLufs">Loudness per ITU-R BS.1770-4 (K-weighted); ungated over the active region for very short clips.</param>
///<param name="LufsIsGated">True when the standard two-stage gating was applied.</param>
///<param name="LoudnessRangeLu">EBU R128 loudness range; 0 for files shorter than 3 s.</param>
///<param name="CrestDb">Peak minus active RMS in dB (high = punchy, low = squashed).</param>
///<param name="DcOffset">Mean sample value, -1 to 1.</param>
///<param name="ClippedSamples">Samples inside runs of three or more full-scale values.</param>
///<param name="LeadSilenceMs">Time before the sound rises above 60 dB below its peak.</param>
///<param name="TrailSilenceMs">Time after the sound last exceeds 60 dB below its peak.</param>
///<param name="ActiveSeconds">Length of the audible part of the file.</param>
///<param name="AttackMs">Time for the envelope to rise from 10 % to 90 % of its peak.</param>
///<param name="DecayMs">Time from the envelope peak until it has fallen 30 dB (or until the end when it never does).</param>
///<param name="Shape">Envelope archetype.</param>
public sealed record DynamicsFacet(
    double PeakDb,
    double TruePeakDb,
    double RmsDb,
    double ActiveRmsDb,
    double IntegratedLufs,
    bool LufsIsGated,
    double LoudnessRangeLu,
    double CrestDb,
    double DcOffset,
    int ClippedSamples,
    double LeadSilenceMs,
    double TrailSilenceMs,
    double ActiveSeconds,
    double AttackMs,
    double DecayMs,
    EnvelopeShape Shape)
{
    #region Public properties
    ///<summary>
    ///Loudness bucket for browsing. The cut points (-18, -14.5 and -11.5 LUFS) are the quartiles of the bundled corpus, because
    ///nearly every sample is peak-normalised and an absolute scale would put most of them in one bucket.
    ///</summary>
    [JsonIgnore]
    public LoudnessClass Loudness =>
        IntegratedLufs switch
        {
            < -18.0 => LoudnessClass.Quiet,
            < -14.5 => LoudnessClass.Moderate,
            < -11.5 => LoudnessClass.Loud,
            _ => LoudnessClass.VeryLoud,
        };
    #endregion
}
