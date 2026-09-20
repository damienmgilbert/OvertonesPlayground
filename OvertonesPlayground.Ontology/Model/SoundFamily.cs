namespace OvertonesPlayground.Ontology.Model;

///<summary>
///Sounds that sound alike: a cluster in the acoustic fingerprint space, found from the audio alone without looking at
///file names. Where kits and variation sets say what a sound was <em>called</em> or <em>made on</em>, a family says
///what it ///<em>sounds like</em>, so it groups a snare from one kit with a similar one from another.
///</summary>
public sealed class SoundFamily : SampleCollection
{
    #region Constants

    ///<summary>
    ///A family is named after its dominant instrument only when at least this share of its members carry it.
    ///</summary>
    public const double MinNamedPurity = 0.5;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates a family.
    ///</summary>
    ///<param name="medoid">The member nearest the middle of the cluster, the most typical sound of the family.</param>
    ///<param name="dominantInstrument">The instrument family (kick, snare, hi-hat ...) most members belong to, or null when there is none to name.</param>
    ///<param name="purity">Fraction of the members that belong to <paramref name="dominantInstrument"/> (0 - 1).</param>
    ///<param name="members">The sounds, by name.</param>
    public SoundFamily(Sample medoid, InstrumentConcept? dominantInstrument, double purity, IReadOnlyList<Sample> members) : base($"{(dominantInstrument is not null && purity >= MinNamedPurity ? dominantInstrument.DisplayName : "Mixed")} like {medoid.Name}", members)
    {
        Medoid = medoid;
        DominantInstrument = dominantInstrument;
        Purity = purity;
    }
    #endregion

    #region Public properties
    ///<summary>
    ///The instrument family (kick, snare, hi-hat ...) most members belong to, or null when there is none to count.
    ///</summary>
    public InstrumentConcept? DominantInstrument { get; }

    ///<inheritdoc/>
    public override string Kind => "Family";

    ///<summary>
    ///The most typical member: the one nearest the middle of the cluster.
    ///</summary>
    public Sample Medoid { get; }

    ///<summary>
    ///How uniform the family is by instrument: the share of members that belong to <see cref="DominantInstrument"/>.
    ///The clustering never sees the labels, so this measures how well sound and name agree.
    ///</summary>
    public double Purity { get; }
    #endregion
}
