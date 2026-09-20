namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///A strategy that inspects one kind of evidence and proposes labels. Classifiers never decide; they vote, and
///<see cref="EvidenceFusion"/> reconciles the votes.
///</summary>
public interface ISampleClassifier
{
    ///<summary>Proposes labels for the sample in <paramref name="context"/>.</summary>
    IReadOnlyList<ClassificationProposal> Classify(ClassificationContext context);
}
