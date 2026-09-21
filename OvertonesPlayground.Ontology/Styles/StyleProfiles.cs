using System.Text.Json;

namespace OvertonesPlayground.Ontology.Styles;

///<summary>
///The style profiles, loaded once from the <c>Data/styles.json</c> embedded in this assembly and checked as they load: every
///pattern must parse, every scale and progression must be known, and every style must name a style of the taxonomy.
///</summary>
public static class StyleProfiles
{
    #region Constants
    private const string ResourceName = "OvertonesPlayground.Ontology.Data.styles.json";
    #endregion

    #region Fields
    private static readonly Lazy<IReadOnlyList<StyleProfile>> _all = new(Load);
    #endregion

    #region Private methods
    private static IReadOnlyList<StyleProfile> Load()
    {
        using Stream stream = typeof(StyleProfiles).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException($"Embedded resource '{ResourceName}' is missing.");
        StyleProfileFile file = JsonSerializer.Deserialize(stream, OntologyDataJsonContext.Default.StyleProfileFile)
            ?? throw new InvalidDataException($"Embedded resource '{ResourceName}' is empty.");

        HashSet<string> keys = new(StringComparer.Ordinal);
        HashSet<string> presetIds = new(StringComparer.Ordinal);
        foreach (StyleProfile profile in file.Profiles)
        {
            Validate(profile, keys, presetIds);
        }

        return file.Profiles;
    }

    private static void Validate(StyleProfile profile, HashSet<string> keys, HashSet<string> presetIds)
    {
        string where = $"styles.json, style '{profile.Key}'";
        if (!keys.Add(profile.Key))
        {
            throw new InvalidDataException($"{where}: the key is used twice.");
        }

        if (!Taxonomies.Styles.TryGet(profile.Style, out _))
        {
            throw new InvalidDataException($"{where}: '{profile.Style}' is not a style of the taxonomy.");
        }

        if (profile.ScaleList.Count != profile.Scales.Count || profile.Scales.Count == 0)
        {
            throw new InvalidDataException($"{where}: a scale is unknown (known: {string.Join(", ", Theory.Scales.All.Select(s => s.Key))}).");
        }

        foreach (string progression in profile.Progressions)
        {
            _ = Progression.Parse(progression);
        }

        if (profile.Kick.Count == 0 || profile.Backbeat.Count == 0 || profile.Hats.Count == 0)
        {
            throw new InvalidDataException($"{where}: it needs at least one kick, backbeat and hat pattern.");
        }

        foreach (string pattern in profile.Kick.Concat(profile.Backbeat).Concat(profile.Hats).Concat(profile.ExtraPatterns))
        {
            _ = StepPattern.Parse(pattern);
        }

        foreach (StylePreset preset in profile.Presets)
        {
            if (!presetIds.Add(preset.Id))
            {
                throw new InvalidDataException($"{where}: preset id '{preset.Id}' is used twice.");
            }

            _ = Key.Parse(preset.Key);
        }
    }
    #endregion

    #region Public methods
    ///<summary>
    ///The profile with key <paramref name="key"/>, or null.
    ///</summary>
    public static StyleProfile? Find(string? key) => All.FirstOrDefault(profile => string.Equals(profile.Key, key, StringComparison.Ordinal));

    ///<summary>
    ///The profile with key <paramref name="key"/>.
    ///</summary>
    ///<exception cref="KeyNotFoundException">There is no such profile.</exception>
    public static StyleProfile Get(string key) => Find(key) ?? throw new KeyNotFoundException($"There is no style profile called '{key}'.");
    #endregion

    #region Public properties
    ///<summary>
    ///Every style profile, in the order of <c>styles.json</c>.
    ///</summary>
    public static IReadOnlyList<StyleProfile> All => _all.Value;

    ///<summary>
    ///Every ready-made setup of every style, with its style.
    ///</summary>
    public static IEnumerable<(StyleProfile Profile, StylePreset Preset)> Presets => All.SelectMany(profile => profile.Presets.Select(preset => (profile, preset)));
    #endregion
}
