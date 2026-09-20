using System.Reflection;
using System.Text.Json;

namespace OvertonesPlayground.Ontology.Concepts;

///<summary>
///The three concept trees (instruments, kits, styles) and the keyword lexicon, loaded once from the JSON embedded in
///this assembly. The data lives in <c>Data/taxonomy.json</c> and <c>Data/lexicon.json</c>, not in code.
///</summary>
public static class Taxonomies
{
    #region Constants
    private const string DataPrefix = "OvertonesPlayground.Ontology.Data.";

    private static readonly Lazy<TaxonomyFile> _taxonomyFile = new(() => LoadEmbedded(DataPrefix + "taxonomy.json", OntologyDataJsonContext.Default.TaxonomyFile));
    private static readonly Lazy<Taxonomy<InstrumentConcept>> _instruments = new(() => Build(_taxonomyFile.Value.Instruments, (node, aliases) => new InstrumentConcept(node.Key, node.Name, aliases, ParseEnum<ContentType>(node.Content), node.Distinctive)));
    private static readonly Lazy<Taxonomy<KitConcept>> _kits = new(() => Build(_taxonomyFile.Value.Kits, (node, aliases) => new KitConcept(node.Key, node.Name, aliases, ParseEnum<SoundOrigin>(node.Origin))));
    private static readonly Lazy<Taxonomy<StyleConcept>> _styles = new(() => Build(_taxonomyFile.Value.Styles, (node, aliases) => new StyleConcept(node.Key, node.Name, aliases)));
    private static readonly Lazy<Lexicon> _lexicon = new(() => new Lexicon(LoadEmbedded(DataPrefix + "lexicon.json", OntologyDataJsonContext.Default.LexiconFile)));
    #endregion

    #region Private methods
    ///<summary>Creates concepts for <paramref name="rootNode"/> and everything below it, wiring parent and child links.</summary>
    private static Taxonomy<T> Build<T>(ConceptNode rootNode, Func<ConceptNode, IReadOnlyList<string>, T> factory)
        where T : OntologyConcept
    {
        List<T> all = [];
        HashSet<string> keys = [];

        T Create(ConceptNode node, T? parent)
        {
            bool isDuplicate = !keys.Add(node.Key);
            if (isDuplicate)
            {
                throw new InvalidDataException($"Duplicate concept key '{node.Key}' in taxonomy.json.");
            }

            T concept = factory(node, node.Aliases);
            all.Add(concept);
            parent?.Attach(concept);
            foreach (ConceptNode child in node.Children)
            {
                _ = Create(child, concept);
            }

            return concept;
        }

        T root = Create(rootNode, null);
        return new Taxonomy<T>(root, all);
    }

    private static T LoadEmbedded<T>(string resourceName, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        Assembly assembly = typeof(Taxonomies).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException($"Embedded resource '{resourceName}' is missing.");
        return JsonSerializer.Deserialize(stream, typeInfo)
            ?? throw new InvalidDataException($"Embedded resource '{resourceName}' is empty.");
    }

    private static T? ParseEnum<T>(string? text)
        where T : struct, Enum
    {
        bool isMissing = string.IsNullOrWhiteSpace(text);
        return isMissing ? null : Enum.Parse<T>(text!, ignoreCase: true);
    }
    #endregion

    #region Public properties
    ///<summary>Instrument concepts (category, family, refinement).</summary>
    public static Taxonomy<InstrumentConcept> Instruments => _instruments.Value;

    ///<summary>Kit, machine and pack concepts.</summary>
    public static Taxonomy<KitConcept> Kits => _kits.Value;

    ///<summary>Keyword lists for content type and origin.</summary>
    public static Lexicon Lexicon => _lexicon.Value;

    ///<summary>Music style concepts.</summary>
    public static Taxonomy<StyleConcept> Styles => _styles.Value;
    #endregion
}
