namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///A sample whose audio-based family prediction differs from its (confident) file-name family.
///</summary>
///<param name="Sample">The sample.</param>
///<param name="ActualFamily">Family key implied by the name.</param>
///<param name="PredictedFamily">Family key the nearest neighbours vote for.</param>
///<param name="Confidence">Strength of the vote.</param>
public sealed record SignalDisagreement(Sample Sample, string ActualFamily, string PredictedFamily, double Confidence);
