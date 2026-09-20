using System.Text.Json;
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

///<summary>
///Reads and writes <see cref="SampleCatalog"/> files. The written file has one sample per line so re-analysing a few
///files produces a small, reviewable diff.
///</summary>
public static class SampleCatalogSerializer
{
    #region Public methods
    ///<summary>Reads a catalog and checks its schema version.</summary>
    public static SampleCatalog Deserialize(Stream stream)
    {
        SampleCatalog catalog = JsonSerializer.Deserialize(stream, SampleCatalogJsonContext.Default.SampleCatalog)
            ?? throw new InvalidDataException("The sample catalog is empty.");
        bool isWrongSchema = catalog.SchemaVersion != SampleCatalog.CurrentSchemaVersion;
        return isWrongSchema
            ? throw new InvalidDataException($"Unsupported sample catalog schema {catalog.SchemaVersion}; expected {SampleCatalog.CurrentSchemaVersion}.")
            : catalog;
    }

    ///<summary>Reads a catalog from UTF-8 JSON text.</summary>
    public static SampleCatalog Deserialize(string json)
    {
        using MemoryStream stream = new(System.Text.Encoding.UTF8.GetBytes(json));
        return Deserialize(stream);
    }

    ///<summary>Writes <paramref name="catalog"/> as UTF-8 JSON, one sample per line.</summary>
    public static void Serialize(SampleCatalog catalog, Stream stream)
    {
        using StreamWriter writer = new(stream, new System.Text.UTF8Encoding(false), 64 * 1024, leaveOpen: true);
        writer.Write($"{{\"schemaVersion\":{catalog.SchemaVersion},\"analyzerVersion\":{JsonSerializer.Serialize(catalog.AnalyzerVersion)},\"samples\":[\n");
        for (int i = 0; i < catalog.Samples.Count; i++)
        {
            writer.Write(JsonSerializer.Serialize(catalog.Samples[i], SampleCatalogJsonContext.Default.Sample));
            writer.Write(i < catalog.Samples.Count - 1 ? ",\n" : "\n");
        }

        writer.Write("]}\n");
    }

    ///<summary>Writes <paramref name="catalog"/> to <paramref name="path"/>, creating the folder if needed.</summary>
    public static void Save(SampleCatalog catalog, string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        using FileStream stream = File.Create(path);
        Serialize(catalog, stream);
    }
    #endregion
}
