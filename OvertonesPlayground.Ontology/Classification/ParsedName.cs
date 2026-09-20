namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///A file name split into the words that can name a concept and the attributes written in it.
///</summary>
///<param name="Words">Normalized words left after tempo, key and variation number were taken out.</param>
///<param name="Attributes">Tempo, key, octave, variation number and stem.</param>
public sealed record ParsedName(string[] Words, NamedAttributes Attributes);
