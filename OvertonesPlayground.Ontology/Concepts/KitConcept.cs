namespace OvertonesPlayground.Ontology.Concepts;

///<summary>
///A kit, drum machine, sampler or sample-pack series that a sound comes from.
///</summary>
public sealed class KitConcept : OntologyConcept
{
    #region Constructors

    ///<summary>
    ///Creates a kit concept.
    ///</summary>
    public KitConcept(string key, string displayName, IReadOnlyList<string> aliases, SoundOrigin? origin) : base(key, displayName, aliases) { OriginOverride = origin; }
    #endregion

    #region Private properties
    private SoundOrigin? OriginOverride { get; }
    #endregion

    #region Public properties
    ///<summary>
    ///How sounds of this kit were produced; inherited from ancestors, <see cref="SoundOrigin.Unknown"/> if none says.
    ///</summary>
    public SoundOrigin Origin => AncestorsAndSelf().OfType<KitConcept>().Select(concept => concept.OriginOverride).FirstOrDefault(origin => origin is not null) ?? SoundOrigin.Unknown;
    #endregion
}
