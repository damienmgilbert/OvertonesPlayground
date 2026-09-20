namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///One classifier's vote: "along <see cref="Axis"/> I think it is <see cref="Value"/>, this sure, for this reason".
///</summary>
///<param name="Axis">The dimension being voted on.</param>
///<param name="Value">A concept key or an enum member name.</param>
///<param name="Confidence">0 - 1.</param>
///<param name="Evidence">Why.</param>
public sealed record ClassificationProposal(ClassificationAxis Axis, string Value, double Confidence, Evidence Evidence);
