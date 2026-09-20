namespace OvertonesPlayground.Ontology.Model;

///<summary>
///A typed edge from one sample to another.
///</summary>
///<param name="Type">Kind of relation.</param>
///<param name="Target">The related sample.</param>
///<param name="Strength">0 - 1; 1 is the strongest.</param>
public sealed record SampleRelationship(SampleRelationType Type, Sample Target, double Strength);
