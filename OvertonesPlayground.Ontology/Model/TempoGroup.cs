namespace OvertonesPlayground.Ontology.Model;

///<summary>
///Samples whose tempos match (within a tolerance, allowing half and double time).
///</summary>
public sealed class TempoGroup : SampleCollection
{
    #region Constructors

    ///<summary>
    ///Creates a tempo group.
    ///</summary>
    public TempoGroup(double bpm, IReadOnlyList<Sample> members) : base(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{bpm:0} BPM"), members) { Bpm = bpm; }
    #endregion

    #region Public properties
    ///<summary>
    ///Reference tempo.
    ///</summary>
    public double Bpm { get; }

    ///<inheritdoc/>
    public override string Kind => "Tempo";
    #endregion
}
