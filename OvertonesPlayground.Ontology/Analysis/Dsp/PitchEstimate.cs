namespace OvertonesPlayground.Ontology.Analysis.Dsp;

///<summary>
///Result of a pitch estimate.
///</summary>
///<param name="FrequencyHz">Estimated fundamental.</param>
///<param name="Confidence">0 - 1; one minus the cumulative-mean-normalized difference at the chosen lag.</param>
///<param name="HarmonicToNoiseDb">10 log10(r / (1 - r)) where r is the normalized autocorrelation at the chosen lag.</param>
public readonly record struct PitchEstimate(double FrequencyHz, double Confidence, double HarmonicToNoiseDb);
