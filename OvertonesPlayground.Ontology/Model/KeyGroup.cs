namespace OvertonesPlayground.Ontology.Model;

///<summary>
///Samples that share a pitch class, so they can be played together.
///</summary>
public sealed class KeyGroup : SampleCollection
{
    #region Constructors

    ///<summary>
    ///Creates a key group.
    ///</summary>
    public KeyGroup(int pitchClass, IReadOnlyList<Sample> members) : base(MusicalNotes.PitchClassName(pitchClass), members) { PitchClass = pitchClass; }
    #endregion

    #region Public properties
    ///<inheritdoc/>
    public override string Kind => "Key";

    ///<summary>
    ///Pitch class, 0 = C.
    ///</summary>
    public int PitchClass { get; }
    #endregion
}
