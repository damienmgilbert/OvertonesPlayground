namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///A sample whose audio-based family prediction differs from its (confident) file-name family.
///</summary>
///<param name="Sample">The sample.</param>
///<param name="ActualFamily">Family key implied by the name.</param>
///<param name="PredictedFamily">Family key the nearest neighbours vote for.</param>
///<param name="Confidence">Strength of the vote.</param>
public sealed record SignalDisagreement(Sample Sample, string ActualFamily, string PredictedFamily, double Confidence);

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
    #region Public properties
    ///<summary>Share of evaluated samples predicted correctly.</summary>
    public double Accuracy => Evaluated == 0 ? 0 : (double)Agreed / Evaluated;

    ///<summary>
    ///Share predicted correctly among distinctive families only. Generic buckets (FX, electronic percussion, foley) are naming
    ///conventions rather than acoustic classes, so this is the fairer measure of how well the audio matches the names.
    ///</summary>
    public double CoreAccuracy => CoreEvaluated == 0 ? 0 : (double)CoreAgreed / CoreEvaluated;
    #endregion
}

///<summary>
///Classifies a whole corpus in two passes. Pass one uses only the evidence that needs no training (file names and
///embedded metadata). The confidently labelled files then teach the signal classifier, and pass two fuses every
///classifier's votes, so the audio can cross-check the names and label the files whose names say nothing.
///Operates on samples that already have their acoustic facets, so it can be re-run from a saved catalog without
///decoding any audio.
///</summary>
public sealed class CorpusClassifier
{
    #region Constants
    private const double TrainingConfidence = 0.8;
    #endregion

    #region Fields
    private readonly ClassificationOverrides _overrides;
    private readonly EvidenceFusion _fusion = new();
    private readonly FilenameLexiconClassifier _filename = new();
    private readonly MetadataClassifier _metadata = new();
    #endregion

    #region Constructors
    ///<summary>Creates a classifier that applies <paramref name="overrides"/> last.</summary>
    public CorpusClassifier(ClassificationOverrides? overrides = null)
    {
        _overrides = overrides ?? ClassificationOverrides.Empty;
    }
    #endregion

    #region Private methods
    private static Sample With(Sample sample, SampleClassification classification) => sample with { Classification = classification };

    private static NamedAttributes AttributesFor(Sample sample, ParsedName parsed) =>
        parsed.Attributes with { TempoBpm = parsed.Attributes.TempoBpm ?? sample.Technical?.EmbeddedTempoBpm };
    #endregion

    #region Public methods
    ///<summary>
    ///Predicts every confidently named sample from the audio of all the others (leave-one-out) and reports how often the
    ///prediction agrees with the name, at the level of the depth-2 instrument family.
    ///</summary>
    public static SignalEvaluation EvaluateSignalAgreement(IReadOnlyList<Sample> classified)
    {
        FingerprintSpace space = new(classified);
        List<TrainingExample> training = [];
        Dictionary<string, Sample> byId = [];
        foreach (Sample sample in classified)
        {
            bool isReliable = sample.Classification.Instrument.Confidence >= TrainingConfidence
                && Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out _)
                && sample.Classification.Instrument.Value != "unclassified";
            double[]? vector = isReliable ? space.Vector(sample) : null;
            if (vector is not null)
            {
                training.Add(new TrainingExample(sample.Id, vector, Taxonomies.Instruments.Get(sample.Classification.Instrument.Value).Family.Key));
                byId[sample.Id] = sample;
            }
        }

        SignalHeuristicClassifier signal = new(space, training);
        int evaluated = 0;
        int agreed = 0;
        int coreEvaluated = 0;
        int coreAgreed = 0;
        List<SignalDisagreement> disagreements = [];
        foreach (TrainingExample example in training)
        {
            Sample sample = byId[example.Id];
            (string Family, double Confidence)[] ranked = signal.Predict(sample);
            if (ranked.Length == 0)
            {
                continue;
            }

            evaluated++;
            bool isCore = Taxonomies.Instruments.Get(example.FamilyKey).IsDistinctive;
            coreEvaluated += isCore ? 1 : 0;
            if (ranked[0].Family == example.FamilyKey)
            {
                agreed++;
                coreAgreed += isCore ? 1 : 0;
            }
            else
            {
                disagreements.Add(new SignalDisagreement(sample, example.FamilyKey, ranked[0].Family, ranked[0].Confidence));
            }
        }

        return new SignalEvaluation(evaluated, agreed, [.. disagreements.OrderByDescending(d => d.Confidence)], coreEvaluated, coreAgreed);
    }

    ///<summary>Returns <paramref name="analyzed"/> with fresh classifications.</summary>
    public IReadOnlyList<Sample> Classify(IReadOnlyList<Sample> analyzed)
    {
        List<(Sample Sample, ParsedName Parsed)> parsed = [];
        foreach (Sample sample in analyzed)
        {
            ParsedName name = FilenameParser.Parse(sample.Name);
            parsed.Add((sample, name with { Attributes = AttributesFor(sample, name) }));
        }

        List<Sample> provisional = [];
        foreach ((Sample sample, ParsedName name) in parsed)
        {
            ClassificationContext context = new(name, sample);
            List<ClassificationProposal> votes = [.. _filename.Classify(context), .. _metadata.Classify(context)];
            provisional.Add(With(sample, _fusion.Fuse(context, votes, null)));
        }

        FingerprintSpace space = new(provisional);
        List<TrainingExample> training = [];
        foreach (Sample sample in provisional)
        {
            bool isReliable = sample.Classification.Instrument.Confidence >= TrainingConfidence
                && sample.Classification.Instrument.Value != "unclassified"
                && Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out _);
            double[]? vector = isReliable ? space.Vector(sample) : null;
            if (vector is not null)
            {
                InstrumentConcept concept = Taxonomies.Instruments.Get(sample.Classification.Instrument.Value);
                training.Add(new TrainingExample(sample.Id, vector, concept.Family.Key));
            }
        }

        SignalHeuristicClassifier signal = new(space, training);

        List<Sample> final = [];
        for (int i = 0; i < parsed.Count; i++)
        {
            (Sample sample, ParsedName name) = parsed[i];
            ClassificationContext context = new(name, provisional[i]);
            List<ClassificationProposal> votes = [.. _filename.Classify(context), .. _metadata.Classify(context), .. signal.Classify(context)];
            final.Add(With(sample, _fusion.Fuse(context, votes, _overrides.Find(sample.Id))));
        }

        return final;
    }
    #endregion
}
