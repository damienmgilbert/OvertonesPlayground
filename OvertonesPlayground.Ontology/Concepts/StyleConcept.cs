namespace OvertonesPlayground.Ontology.Concepts;

///<summary>
///A musical style or genre: Hip-Hop, House, Funk / Soul / Disco ...
///</summary>
public sealed class StyleConcept : OntologyConcept
{
    ///<summary>Creates a style concept.</summary>
    public StyleConcept(string key, string displayName, IReadOnlyList<string> aliases)
        : base(key, displayName, aliases)
    {
    }
}
