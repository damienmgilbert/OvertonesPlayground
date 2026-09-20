namespace OvertonesPlayground.Ontology.Concepts;

///<summary>
///A kind of sound source: Kick, Closed Hi-Hat, Electric Piano, Riser ...
///Depth 1 is the category (Percussion, Keys ...), depth 2 the family (Kick, Snare ...), deeper levels are refinements.
///</summary>
public sealed class InstrumentConcept : OntologyConcept
{
    #region Constructors
    ///<summary>Creates an instrument concept.</summary>
    public InstrumentConcept(string key, string displayName, IReadOnlyList<string> aliases, ContentType? content, bool distinctive = false)
        : base(key, displayName, aliases)
    {
        ContentOverride = content;
        DistinctiveOverride = distinctive;
    }
    #endregion

    #region Public properties
    ///<summary>Content type this concept implies when the file name says nothing else; inherited from ancestors.</summary>
    public ContentType DefaultContentType =>
        AncestorsAndSelf().OfType<InstrumentConcept>().Select(concept => concept.ContentOverride).FirstOrDefault(content => content is not null) ?? ContentType.OneShot;

    ///<summary>The depth-2 ancestor used for coarse "family" comparisons.</summary>
    public InstrumentConcept Family => (InstrumentConcept)AtDepth(2);

    ///<summary>The depth-1 ancestor (Percussion, Keys ...).</summary>
    public InstrumentConcept Category => (InstrumentConcept)AtDepth(1);

    ///<summary>
    ///True for families with a well-defined sound (kick, snare, hi-hat ...), where a mismatch between the name and the audio is
    ///worth a human look. Generic families (FX, electronic percussion, stabs) sound like almost anything, so their audio
    ///says little about the name. Inherited from ancestors.
    ///</summary>
    public bool IsDistinctive => AncestorsAndSelf().OfType<InstrumentConcept>().Any(concept => concept.DistinctiveOverride);

    private ContentType? ContentOverride { get; }

    private bool DistinctiveOverride { get; }
    #endregion
}

///<summary>
///A musical style or genre: Hip-Hop, House, Funk / Soul / Disco ...
///</summary>
public sealed class StyleConcept : OntologyConcept
{
    #region Constructors
    ///<summary>Creates a style concept.</summary>
    public StyleConcept(string key, string displayName, IReadOnlyList<string> aliases)
        : base(key, displayName, aliases)
    {
    }
    #endregion
}

///<summary>
///A kit, drum machine, sampler or sample-pack series that a sound comes from.
///</summary>
public sealed class KitConcept : OntologyConcept
{
    #region Constructors
    ///<summary>Creates a kit concept.</summary>
    public KitConcept(string key, string displayName, IReadOnlyList<string> aliases, SoundOrigin? origin)
        : base(key, displayName, aliases)
    {
        OriginOverride = origin;
    }
    #endregion

    #region Public properties
    ///<summary>How sounds of this kit were produced; inherited from ancestors, <see cref="SoundOrigin.Unknown"/> if none says.</summary>
    public SoundOrigin Origin =>
        AncestorsAndSelf().OfType<KitConcept>().Select(concept => concept.OriginOverride).FirstOrDefault(origin => origin is not null) ?? SoundOrigin.Unknown;

    private SoundOrigin? OriginOverride { get; }
    #endregion
}
