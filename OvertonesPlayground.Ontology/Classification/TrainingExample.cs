namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///A sample whose family is already known, used to teach the nearest-neighbour vote.
///</summary>
///<param name="Id">Sample id, so a sample never votes for itself.</param>
///<param name="Vector">Standardized acoustic fingerprint.</param>
///<param name="FamilyKey">Key of the depth-2 instrument concept.</param>
public sealed record TrainingExample(string Id, double[] Vector, string FamilyKey);
