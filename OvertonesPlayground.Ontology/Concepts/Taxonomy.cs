using System.Diagnostics.CodeAnalysis;

namespace OvertonesPlayground.Ontology.Concepts;

///<summary>
///One way a concept can be named in a file name: a run of normalized words.
///</summary>
///<typeparam name="TConcept">Concept kind.</typeparam>
///<param name="Concept">The concept the words name.</param>
///<param name="Tokens">The words, lower-case.</param>
///<param name="StartOnly">The words only count at the very start of the name (written <c>^808</c> in the data).</param>
public sealed record ConceptAlias<TConcept>(TConcept Concept, string[] Tokens, bool StartOnly)
    where TConcept : OntologyConcept;

///<summary>
///A rooted tree of concepts with key lookup and an alias index. Built from data by <see cref="Taxonomies"/>.
///</summary>
///<typeparam name="TConcept">Concept kind held by the tree.</typeparam>
public sealed class Taxonomy<TConcept>
    where TConcept : OntologyConcept
{
    #region Fields
    private readonly Dictionary<string, TConcept> _byKey;
    #endregion

    #region Constructors
    ///<summary>Wraps an already wired concept tree.</summary>
    internal Taxonomy(TConcept root, IReadOnlyList<TConcept> all)
    {
        Root = root;
        All = all;
        _byKey = all.ToDictionary(concept => concept.Key, StringComparer.Ordinal);
        Aliases = BuildAliases(all);
    }
    #endregion

    #region Private methods
    ///<summary>Flattens every concept alias into token runs; each concept also answers to its own key and display name.</summary>
    private static List<ConceptAlias<TConcept>> BuildAliases(IReadOnlyList<TConcept> all)
    {
        List<ConceptAlias<TConcept>> entries = [];
        foreach (TConcept concept in all)
        {
            bool isRoot = concept.Parent is null;
            if (isRoot)
            {
                continue;
            }

            HashSet<string> seen = [];
            IEnumerable<string> raw = concept.Aliases.Append(concept.Key).Append(concept.DisplayName);
            foreach (string alias in raw)
            {
                bool startOnly = alias.StartsWith('^');
                string[] tokens = TextNormalizer.Tokenize(startOnly ? alias[1..] : alias);
                bool isEmpty = tokens.Length == 0;
                bool isNew = seen.Add((startOnly ? "^" : string.Empty) + string.Join(' ', tokens));
                if (!isEmpty && isNew)
                {
                    entries.Add(new ConceptAlias<TConcept>(concept, tokens, startOnly));
                }
            }
        }

        return entries;
    }
    #endregion

    #region Public methods
    ///<summary>The direct children of <paramref name="parent"/>.</summary>
    public IReadOnlyList<TConcept> ChildrenOf(TConcept parent) => [.. parent.Children.Cast<TConcept>()];

    ///<summary>The concept with <paramref name="key"/>; throws when there is none.</summary>
    public TConcept Get(string key) =>
        _byKey.TryGetValue(key, out TConcept? concept) ? concept : throw new KeyNotFoundException($"No concept '{key}' in the taxonomy.");

    ///<summary>Looks up a concept by key.</summary>
    public bool TryGet(string key, [NotNullWhen(true)] out TConcept? concept) => _byKey.TryGetValue(key, out concept);
    #endregion

    #region Public properties
    ///<summary>Every concept, root first.</summary>
    public IReadOnlyList<TConcept> All { get; }

    ///<summary>Every way a concept can be named; longer runs come first.</summary>
    public IReadOnlyList<ConceptAlias<TConcept>> Aliases { get; }

    ///<summary>Concepts with no children.</summary>
    public IEnumerable<TConcept> Leaves => All.Where(concept => concept.Children.Count == 0);

    ///<summary>The root concept.</summary>
    public TConcept Root { get; }
    #endregion
}
