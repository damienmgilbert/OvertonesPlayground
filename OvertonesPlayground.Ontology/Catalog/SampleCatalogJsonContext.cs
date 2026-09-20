using System.Text.Json.Serialization;

namespace OvertonesPlayground.Ontology.Catalog;

///<summary>
///Source-generated (trim-safe, reflection-free) JSON metadata for <see cref="SampleCatalog"/>.
///</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals)]
[JsonSerializable(typeof(SampleCatalog))]
[JsonSerializable(typeof(Sample))]
[JsonSerializable(typeof(Label<string>))]
[JsonSerializable(typeof(Label<ContentType>))]
[JsonSerializable(typeof(Label<SoundOrigin>))]
public sealed partial class SampleCatalogJsonContext : JsonSerializerContext
{
}
