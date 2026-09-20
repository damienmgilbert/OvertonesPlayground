namespace OvertonesPlayground.Ontology.Model;

///<summary>
///One reason a label was assigned.
///</summary>
///<param name="Source">Where the evidence came from.</param>
///<param name="Detail">Human-readable detail, for example the matched word or the neighbours that voted.</param>
public sealed record Evidence(EvidenceSource Source, string Detail);

///<summary>
///A classification value together with how sure we are and why.
///</summary>
///<typeparam name="TValue">A taxonomy concept key (<see cref="string"/>) or an enum.</typeparam>
///<param name="Value">The assigned value.</param>
///<param name="Confidence">0 - 1.</param>
///<param name="Evidence">The evidence that produced it, strongest first.</param>
public sealed record Label<TValue>(TValue Value, double Confidence, IReadOnlyList<Evidence> Evidence);
