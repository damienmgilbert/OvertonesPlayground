namespace OvertonesPlayground.Ontology.Model;

///<summary>
///Samples from one kit or machine (all the Roland 909 sounds, all the Vinyl-sampled drums).
///</summary>
public sealed class Kit : SampleCollection
{
    #region Constructors

    ///<summary>
    ///Creates a kit.
    ///</summary>
    public Kit(KitConcept concept, IReadOnlyList<Sample> members) : base(concept.DisplayName, members) { Concept = concept; }
    #endregion

    #region Public properties
    ///<summary>
    ///The kit concept in the taxonomy.
    ///</summary>
    public KitConcept Concept { get; }

    ///<inheritdoc/>
    public override string Kind => "Kit";
    #endregion
}
