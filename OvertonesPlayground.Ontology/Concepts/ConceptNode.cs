namespace OvertonesPlayground.Ontology.Concepts;

///<summary>
///Shape of one concept in <c>taxonomy.json</c>.
///</summary>
internal sealed class ConceptNode
{
    public List<string> Aliases { get; set; } = [];

    public List<ConceptNode> Children { get; set; } = [];

    public string? Content { get; set; }

    public bool Distinctive { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Origin { get; set; }
}
