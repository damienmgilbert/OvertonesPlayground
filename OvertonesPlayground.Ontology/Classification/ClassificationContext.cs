namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///What a classifier may look at: the name, the parsed name, and the sample with all its facets. The sample's
///classification is a placeholder at this point.
///</summary>
///<param name="Parsed">The file name, parsed.</param>
///<param name="Sample">The sample with facets filled in.</param>
public sealed record ClassificationContext(ParsedName Parsed, Sample Sample)
{
    ///<summary>Display name of the sample.</summary>
    public string Name => Sample.Name;
}
