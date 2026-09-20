namespace OvertonesPlayground.Ontology.Concepts;

///<summary>
///A node in an is-a hierarchy (a snare <em>is a</em> drum-kit piece, which <em>is a</em> percussion instrument).
///Concrete kinds add the data that only makes sense for them.
///</summary>
public abstract class OntologyConcept
{
    #region Fields
    private readonly List<OntologyConcept> _children = [];
    #endregion

    #region Constructors
    ///<summary>
    ///Creates a concept; <see cref="Taxonomy{TConcept}"/> wires up parent and children.
    ///</summary>
    protected OntologyConcept(string key, string displayName, IReadOnlyList<string> aliases)
    {
        Key = key;
        DisplayName = displayName;
        Aliases = aliases;
    }
    #endregion

    #region Internal methods
    ///<summary>
    ///Adds <paramref name="child"/> below this concept.
    ///</summary>
    internal void Attach(OntologyConcept child)
    {
        child.Parent = this;
        _children.Add(child);
    }
    #endregion

    #region Public methods
    ///<summary>
    ///This concept followed by each ancestor up to the root.
    ///</summary>
    public IEnumerable<OntologyConcept> AncestorsAndSelf()
    {
        for (OntologyConcept? current = this; current is not null; current = current.Parent)
        {
            yield return current;
        }
    }

    ///<summary>
    ///The ancestor-or-self at <paramref name="depth"/> (root = 0), or this concept when it is shallower than that.
    ///</summary>
    public OntologyConcept AtDepth(int depth)
    {
        OntologyConcept current = this;
        while (current.Depth > depth && current.Parent is not null)
        {
            current = current.Parent;
        }

        return current;
    }

    ///<summary>
    ///This concept followed by every descendant, depth first.
    ///</summary>
    public IEnumerable<OntologyConcept> DescendantsAndSelf()
    {
        yield return this;
        foreach (OntologyConcept child in _children)
        {
            foreach (OntologyConcept descendant in child.DescendantsAndSelf())
            {
                yield return descendant;
            }
        }
    }

    ///<summary>
    ///True when this concept is <paramref name="other"/> or one of its descendants.
    ///</summary>
    public bool IsA(OntologyConcept other) => AncestorsAndSelf().Any(concept => ReferenceEquals(concept, other));

    ///<summary>
    ///True when this concept is, or descends from, the concept with <paramref name="key"/>.
    ///</summary>
    public bool IsA(string key) => AncestorsAndSelf().Any(concept => concept.Key == key);

    ///<inheritdoc/>
    public override string ToString() => Key;
    #endregion

    #region Public properties
    ///<summary>
    ///Normalized words that name this concept; a leading <c>^</c> means "only as the first word of a file name".
    ///</summary>
    public IReadOnlyList<string> Aliases { get; }

    ///<summary>
    ///Direct sub-concepts.
    ///</summary>
    public IReadOnlyList<OntologyConcept> Children => _children;

    ///<summary>
    ///Distance from the root (root = 0).
    ///</summary>
    public int Depth => Parent is null ? 0 : Parent.Depth + 1;

    ///<summary>
    ///Name for display.
    ///</summary>
    public string DisplayName { get; }

    ///<summary>
    ///Stable identifier, unique within a taxonomy.
    ///</summary>
    public string Key { get; }

    ///<summary>
    ///The parent concept, or null for the root.
    ///</summary>
    public OntologyConcept? Parent { get; private set; }

    ///<summary>
    ///Path from below the root to this concept, for example <c>Percussion &gt; Hi-Hat &gt; Closed Hi-Hat</c>.
    ///</summary>
    public string Path => string.Join(" > ", AncestorsAndSelf().Reverse().Skip(1).Select(concept => concept.DisplayName));
    #endregion
}
