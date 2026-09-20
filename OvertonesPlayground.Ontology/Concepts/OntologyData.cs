using System.Text.Json;
using System.Text.Json.Serialization;

namespace OvertonesPlayground.Ontology.Concepts;

///<summary>
///Shape of one concept in <c>taxonomy.json</c>.
///</summary>
internal sealed class ConceptNode
{
    #region Public properties
    public List<string> Aliases { get; set; } = [];

    public List<ConceptNode> Children { get; set; } = [];

    public string? Content { get; set; }

    public bool Distinctive { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Origin { get; set; }
    #endregion
}

///<summary>
///Shape of <c>taxonomy.json</c>: one tree per taxonomy.
///</summary>
internal sealed class TaxonomyFile
{
    #region Public properties
    public ConceptNode Instruments { get; set; } = new();

    public ConceptNode Kits { get; set; } = new();

    public ConceptNode Styles { get; set; } = new();
    #endregion
}

///<summary>
///Shape of <c>lexicon.json</c>: keyword sets that are not concepts.
///</summary>
internal sealed class LexiconFile
{
    #region Public properties
    public Dictionary<string, List<string>> ContentTypes { get; set; } = [];

    public List<string> IgnoredTokens { get; set; } = [];

    public Dictionary<string, List<string>> Origins { get; set; } = [];
    #endregion
}

///<summary>
///Source-generated (trim-safe) JSON metadata for the embedded ontology data.
///</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(TaxonomyFile))]
[JsonSerializable(typeof(LexiconFile))]
internal sealed partial class OntologyDataJsonContext : JsonSerializerContext
{
}
