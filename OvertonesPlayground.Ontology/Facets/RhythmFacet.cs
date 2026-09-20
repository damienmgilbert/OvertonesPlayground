using System.Text.Json.Serialization;

namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///Rhythmic content. Only computed for files long enough to hold more than a single hit.
///</summary>
///<param name="DetectedBpm">
///Tempo estimated from the onset envelope, or null when nothing periodic was found. When the estimate is confident it
///is corrected to a tempo the length of the clip allows (<see cref="RhythmAnalyzer.SnapToLoopGrid"/>).
///</param>
///<param name="TempoConfidence">0 - 1 strength of the periodicity behind <paramref name="DetectedBpm"/>.</param>
///<param name="OnsetsPerSecond">Rate of detected note / hit onsets.</param>
///<param name="OnsetCount">Number of detected onsets.</param>
///<param name="TempoAgreesWithName">
///Whether the detected tempo is metrically related to the tempo written in the file name: the same, half or double
///time, or a dotted / triplet relation (3:2, 4:3). Detection often lands on the strongest pulse of a drum loop rather
///than its written beat, so this is deliberately not an exact match. Null when either tempo is missing.
///</param>
///<param name="Beats">Length in beats at the best-known tempo, or null.</param>
///<param name="Bars">Length in 4/4 bars at the best-known tempo, or null.</param>
///<param name="IsLoopLike">The clip is periodic and spans a whole number of beats, so it repeats cleanly.</param>
public sealed record RhythmFacet(double? DetectedBpm, double TempoConfidence, double OnsetsPerSecond, int OnsetCount, bool? TempoAgreesWithName, double? Beats, double? Bars, bool IsLoopLike)
{
    #region Constants

    ///<summary>
    ///Smallest <see cref="TempoConfidence"/> at which <see cref="DetectedBpm"/> is treated as a real tempo.
    ///</summary>
    public const double MinTrustedConfidence = 0.30;
    #endregion

    #region Public properties
    ///<summary>
    [JsonIgnore]
    public double? TrustedBpm => DetectedBpm is { } bpm && TempoConfidence >= MinTrustedConfidence ? bpm : null;
    #endregion
}
