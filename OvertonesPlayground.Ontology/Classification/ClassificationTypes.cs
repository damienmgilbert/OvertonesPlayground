namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///The dimensions along which a sample is classified.
///</summary>
public enum ClassificationAxis
{
    ///<summary>What sound source it is (an instrument taxonomy concept key).</summary>
    Instrument,

    ///<summary>Which kit, machine or pack it comes from (a kit taxonomy concept key).</summary>
    Kit,

    ///<summary>The music style (a style taxonomy concept key).</summary>
    Style,

    ///<summary>Its structural role (a <see cref="Model.ContentType"/> name).</summary>
    ContentType,

    ///<summary>How it was produced (a <see cref="Model.SoundOrigin"/> name).</summary>
    Origin,
}

///<summary>
///One classifier's vote: "along <see cref="Axis"/> I think it is <see cref="Value"/>, this sure, for this reason".
///</summary>
///<param name="Axis">The dimension being voted on.</param>
///<param name="Value">A concept key or an enum member name.</param>
///<param name="Confidence">0 - 1.</param>
///<param name="Evidence">Why.</param>
public sealed record ClassificationProposal(ClassificationAxis Axis, string Value, double Confidence, Evidence Evidence);

///<summary>
///What a classifier may look at: the name, the parsed name, and the sample with all its facets. The sample's
///classification is a placeholder at this point.
///</summary>
///<param name="Parsed">The file name, parsed.</param>
///<param name="Sample">The sample with facets filled in.</param>
public sealed record ClassificationContext(ParsedName Parsed, Sample Sample)
{
    #region Public properties
    ///<summary>Display name of the sample.</summary>
    public string Name => Sample.Name;
    #endregion
}

///<summary>
///A strategy that inspects one kind of evidence and proposes labels. Classifiers never decide; they vote, and
///<see cref="EvidenceFusion"/> reconciles the votes.
///</summary>
public interface ISampleClassifier
{
    #region Methods
    ///<summary>Proposes labels for the sample in <paramref name="context"/>.</summary>
    IReadOnlyList<ClassificationProposal> Classify(ClassificationContext context);
    #endregion
}
