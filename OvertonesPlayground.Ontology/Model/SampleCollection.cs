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
