using System.Text.Json;
using System.Text.Json.Serialization;

namespace OvertonesPlayground.Ontology.Concepts;

///<summary>
///Source-generated (trim-safe) JSON metadata for the embedded ontology data.
///</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(TaxonomyFile))]
[JsonSerializable(typeof(LexiconFile))]
[JsonSerializable(typeof(OvertonesPlayground.Ontology.Styles.StyleProfileFile))]
internal sealed partial class OntologyDataJsonContext : JsonSerializerContext
{
}
