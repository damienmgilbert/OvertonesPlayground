using System.Text.Json;

namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///Hand-written corrections applied after all automatic evidence, so a maintainer can fix any file without touching the
///lexicon. Every entry becomes <see cref="EvidenceSource.Manual"/> evidence with full confidence.
///</summary>
public sealed class ClassificationOverrides
{
    private readonly Dictionary<string, OverrideEntry> _byFile;

    ///<summary>Wraps a list of entries.</summary>
    public ClassificationOverrides(IEnumerable<OverrideEntry> entries)
    {
        _byFile = entries.ToDictionary(entry => entry.File, StringComparer.OrdinalIgnoreCase);
    }

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

    ///<summary>No overrides.</summary>
    public static ClassificationOverrides Empty { get; } = new([]);

    ///<summary>Number of entries.</summary>
    public int Count => _byFile.Count;
}
