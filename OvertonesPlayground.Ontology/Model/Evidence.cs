namespace OvertonesPlayground.Ontology.Model;

///<summary>
///One reason a label was assigned.
///</summary>
///<param name="Source">Where the evidence came from.</param>
///<param name="Detail">Human-readable detail, for example the matched word or the neighbours that voted.</param>
public sealed record Evidence(EvidenceSource Source, string Detail);
