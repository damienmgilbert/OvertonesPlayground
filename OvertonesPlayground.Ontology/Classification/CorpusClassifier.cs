namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///Classifies a whole corpus in two passes. Pass one uses only the evidence that needs no training (file names and
///embedded metadata). The confidently labelled files then teach the signal classifier, and pass two fuses every
///classifier's votes, so the audio can cross-check the names and label the files whose names say nothing.
///Operates on samples that already have their acoustic facets, so it can be re-run from a saved catalog without
///decoding any audio.
///</summary>
public sealed class CorpusClassifier
{
    private const double TrainingConfidence = 0.8;

    private readonly ClassificationOverrides _overrides;
    private readonly EvidenceFusion _fusion = new();
    private readonly FilenameLexiconClassifier _filename = new();
    private readonly MetadataClassifier _metadata = new();

    ///<summary>Creates a classifier that applies <paramref name="overrides"/> last.</summary>
    public CorpusClassifier(ClassificationOverrides? overrides = null)
    {
        _overrides = overrides ?? ClassificationOverrides.Empty;
    }

    private static Sample With(Sample sample, SampleClassification classification) => sample with { Classification = classification };

    private static NamedAttributes AttributesFor(Sample sample, ParsedName parsed) =>
        parsed.Attributes with { TempoBpm = parsed.Attributes.TempoBpm ?? sample.Technical?.EmbeddedTempoBpm };

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
}
