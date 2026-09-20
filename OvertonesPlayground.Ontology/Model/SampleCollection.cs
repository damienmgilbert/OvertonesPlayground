namespace OvertonesPlayground.Ontology.Model;

///<summary>
///A named group of samples that belong together. Concrete kinds say <em>why</em> they belong together.
///</summary>
public abstract class SampleCollection
{
    #region Constructors
    ///<summary>Creates a collection.</summary>
    protected SampleCollection(string name, IReadOnlyList<Sample> members)
    {
        Name = name;
        Members = members;
    }
    #endregion

    #region Public properties
    ///<summary>Why the members are grouped, for display.</summary>
    public abstract string Kind { get; }

    ///<summary>The samples in the collection.</summary>
    public IReadOnlyList<Sample> Members { get; }

    ///<summary>Display name.</summary>
    public string Name { get; }
    #endregion
}

///<summary>
///Samples from one kit or machine (all the Roland 909 sounds, all the Vinyl-sampled drums).
///</summary>
public sealed class Kit : SampleCollection
{
    #region Constructors
    ///<summary>Creates a kit.</summary>
    public Kit(KitConcept concept, IReadOnlyList<Sample> members)
        : base(concept.DisplayName, members)
    {
        Concept = concept;
    }
    #endregion

    #region Public properties
    ///<summary>The kit concept in the taxonomy.</summary>
    public KitConcept Concept { get; }

    ///<inheritdoc/>
    public override string Kind => "Kit";
    #endregion
}

///<summary>
///Numbered variations of one sound (<c>Kick Vinyl 1</c>, <c>Kick Vinyl 2</c> ...), sharing a name stem.
///</summary>
public sealed class VariationSet : SampleCollection
{
    #region Constructors
    ///<summary>Creates a variation set.</summary>
    public VariationSet(string stem, IReadOnlyList<Sample> members)
        : base(stem, members)
    {
    }
    #endregion

    #region Public properties
    ///<inheritdoc/>
    public override string Kind => "Variations";
    #endregion
}

///<summary>
///Samples whose tempos match (within a tolerance, allowing half and double time).
///</summary>
public sealed class TempoGroup : SampleCollection
{
    #region Constructors
    ///<summary>Creates a tempo group.</summary>
    public TempoGroup(double bpm, IReadOnlyList<Sample> members)
        : base(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{bpm:0} BPM"), members)
    {
        Bpm = bpm;
    }
    #endregion

    #region Public properties
    ///<summary>Reference tempo.</summary>
    public double Bpm { get; }

    ///<inheritdoc/>
    public override string Kind => "Tempo";
    #endregion
}

///<summary>
///Samples that share a pitch class, so they can be played together.
///</summary>
public sealed class KeyGroup : SampleCollection
{
    #region Constructors
    ///<summary>Creates a key group.</summary>
    public KeyGroup(int pitchClass, IReadOnlyList<Sample> members)
        : base(MusicalNotes.PitchClassName(pitchClass), members)
    {
        PitchClass = pitchClass;
    }
    #endregion

    #region Public properties
    ///<inheritdoc/>
    public override string Kind => "Key";

    ///<summary>Pitch class, 0 = C.</summary>
    public int PitchClass { get; }
    #endregion
}

///<summary>
///Sounds that sound alike: a cluster in the acoustic fingerprint space, found from the audio alone without looking at file
///names. Where kits and variation sets say what a sound was <em>called</em> or <em>made on</em>, a family says what it
///<em>sounds like</em>, so it groups a snare from one kit with a similar one from another.
///</summary>
public sealed class SoundFamily : SampleCollection
{
    #region Constants
    ///<summary>A family is named after its dominant instrument only when at least this share of its members carry it.</summary>
    public const double MinNamedPurity = 0.5;
    #endregion

    #region Constructors
    ///<summary>Creates a family.</summary>
    ///<param name="medoid">The member nearest the middle of the cluster, the most typical sound of the family.</param>
    ///<param name="dominantInstrument">The instrument family (kick, snare, hi-hat ...) most members belong to, or null when there is none to name.</param>
    ///<param name="purity">Fraction of the members that belong to <paramref name="dominantInstrument"/> (0 - 1).</param>
    ///<param name="members">The sounds, by name.</param>
    public SoundFamily(Sample medoid, InstrumentConcept? dominantInstrument, double purity, IReadOnlyList<Sample> members)
        : base((dominantInstrument is not null && purity >= MinNamedPurity ? dominantInstrument.DisplayName : "Mixed") + " like " + medoid.Name, members)
    {
        Medoid = medoid;
        DominantInstrument = dominantInstrument;
        Purity = purity;
    }
    #endregion

    #region Public properties
    ///<summary>The instrument family (kick, snare, hi-hat ...) most members belong to, or null when there is none to count.</summary>
    public InstrumentConcept? DominantInstrument { get; }

    ///<inheritdoc/>
    public override string Kind => "Family";

    ///<summary>The most typical member: the one nearest the middle of the cluster.</summary>
    public Sample Medoid { get; }

    ///<summary>
    ///How uniform the family is by instrument: the share of members that belong to <see cref="DominantInstrument"/>. The
    ///clustering never sees the labels, so this measures how well sound and name agree.
    ///</summary>
    public double Purity { get; }
    #endregion
}
