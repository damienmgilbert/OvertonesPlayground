namespace OvertonesPlayground.Ontology.Concepts;

///<summary>
///Shape of <c>lexicon.json</c>: keyword sets that are not concepts.
///</summary>
internal sealed class LexiconFile
{
    public Dictionary<string, List<string>> ContentTypes { get; set; } = [];

    public List<string> IgnoredTokens { get; set; } = [];

    public Dictionary<string, List<string>> Origins { get; set; } = [];
}
