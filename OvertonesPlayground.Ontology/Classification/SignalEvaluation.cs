namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///Leave-one-out check of how well the audio alone predicts what the file names say.
///</summary>
///<param name="Evaluated">Samples with a confident name label that could be predicted.</param>
///<param name="Agreed">Samples where the prediction matched.</param>
///<param name="Disagreements">The mismatches, most confident first.</param>
///<param name="CoreEvaluated">How many of the evaluated samples belong to a distinctive family (kick, snare, hi-hat ...).</param>
///<param name="CoreAgreed">How many of those were predicted correctly.</param>
public sealed record SignalEvaluation(int Evaluated, int Agreed, IReadOnlyList<SignalDisagreement> Disagreements, int CoreEvaluated, int CoreAgreed)
{
    ///<summary>Share of evaluated samples predicted correctly.</summary>
    public double Accuracy => Evaluated == 0 ? 0 : (double)Agreed / Evaluated;

    ///<summary>
    ///Share predicted correctly among distinctive families only. Generic buckets (FX, electronic percussion, foley) are naming
    ///conventions rather than acoustic classes, so this is the fairer measure of how well the audio matches the names.
    ///</summary>
    public double CoreAccuracy => CoreEvaluated == 0 ? 0 : (double)CoreAgreed / CoreEvaluated;
}
