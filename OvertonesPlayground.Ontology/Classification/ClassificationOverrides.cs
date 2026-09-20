using System.Text.Json;
using System.Text.Json.Serialization;

namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///A hand-written correction for one file. Any axis left null keeps the automatic result.
///</summary>
public sealed class OverrideEntry
{
    #region Public properties
    ///<summary>Content type name (<see cref="Model.ContentType"/>).</summary>
    public string? ContentType { get; set; }

    ///<summary>File name with extension.</summary>
    public string File { get; set; } = string.Empty;

    ///<summary>Instrument concept key.</summary>
    public string? Instrument { get; set; }

    ///<summary>Kit concept key.</summary>
    public string? Kit { get; set; }

    ///<summary>Why the override exists.</summary>
    public string? Note { get; set; }

    ///<summary>Sound origin name (<see cref="Model.SoundOrigin"/>).</summary>
    public string? Origin { get; set; }

    ///<summary>Style concept key.</summary>
    public string? Style { get; set; }
    #endregion
}

///<summary>
///Shape of <c>overrides.json</c>.
///</summary>
internal sealed class OverridesFile
{
    #region Public properties
    public List<OverrideEntry> Overrides { get; set; } = [];
    #endregion
}

///<summary>
///Source-generated JSON metadata for <c>overrides.json</c>.
///</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OverridesFile))]
internal sealed partial class OverridesJsonContext : JsonSerializerContext
{
}

///<summary>
///Hand-written corrections applied after all automatic evidence, so a maintainer can fix any file without touching the
///lexicon. Every entry becomes <see cref="EvidenceSource.Manual"/> evidence with full confidence.
///</summary>
public sealed class ClassificationOverrides
{
    #region Fields
    private readonly Dictionary<string, OverrideEntry> _byFile;
    #endregion

    #region Constructors
    ///<summary>Wraps a list of entries.</summary>
    public ClassificationOverrides(IEnumerable<OverrideEntry> entries)
    {
        _byFile = entries.ToDictionary(entry => entry.File, StringComparer.OrdinalIgnoreCase);
    }
    #endregion

    #region Public methods
    ///<summary>Parses <c>overrides.json</c> text.</summary>
    public static ClassificationOverrides FromJson(string json)
    {
        OverridesFile file = JsonSerializer.Deserialize(json, OverridesJsonContext.Default.OverridesFile) ?? new OverridesFile();
        return new ClassificationOverrides(file.Overrides);
    }

    ///<summary>Reads <paramref name="path"/>, or returns <see cref="Empty"/> when it does not exist.</summary>
    public static ClassificationOverrides Load(string path) => File.Exists(path) ? FromJson(File.ReadAllText(path)) : Empty;

    ///<summary>The override for <paramref name="fileName"/>, or null.</summary>
    public OverrideEntry? Find(string fileName) => _byFile.GetValueOrDefault(fileName);
    #endregion

    #region Public properties
    ///<summary>No overrides.</summary>
    public static ClassificationOverrides Empty { get; } = new([]);

    ///<summary>Number of entries.</summary>
    public int Count => _byFile.Count;
    #endregion
}
