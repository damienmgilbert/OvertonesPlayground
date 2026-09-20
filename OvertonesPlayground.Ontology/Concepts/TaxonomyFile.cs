namespace OvertonesPlayground.Ontology.Concepts;

///<summary>
///Shape of <c>taxonomy.json</c>: one tree per taxonomy.
///</summary>
internal sealed class TaxonomyFile
{
    public ConceptNode Instruments { get; set; } = new();

    public ConceptNode Kits { get; set; } = new();

    public ConceptNode Styles { get; set; } = new();
}
