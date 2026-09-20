namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///Votes from the audio itself. It is self-supervised: the ~95 % of files whose names already say what they are become
///training data, and every sample is classified by a distance-weighted vote of its nearest neighbours in the
///standardized feature space (leave-one-out for the training files). It cross-checks the names, fills in files whose
///names say nothing, and classifies content type from duration, rhythm and noisiness.
///</summary>
public sealed class SignalHeuristicClassifier : ISampleClassifier
{
    ///<summary>Neighbours that vote.</summary>
    public const int DefaultNeighbours = 9;

    private const double DistanceSoftening = 0.25;
    private const double ConfidenceCeiling = 0.9;

    private readonly FingerprintSpace _space;
    private readonly IReadOnlyList<TrainingExample> _training;
    private readonly int _neighbours;

    ///<summary>Creates a classifier over labelled examples.</summary>
    public SignalHeuristicClassifier(FingerprintSpace space, IReadOnlyList<TrainingExample> training, int neighbours = DefaultNeighbours)
    {
        _space = space;
        _training = training;
        _neighbours = neighbours;
    }

    ///<inheritdoc/>
    public IReadOnlyList<ClassificationProposal> Classify(ClassificationContext context)
    {
        List<ClassificationProposal> proposals = [];
        Sample sample = context.Sample;

        (string Family, double Confidence)[] ranked = Predict(sample);
        for (int i = 0; i < ranked.Length && i < 2; i++)
        {
            if (i == 0 || ranked[i].Confidence >= 0.2)
            {
                proposals.Add(new ClassificationProposal(
                    ClassificationAxis.Instrument,
                    ranked[i].Family,
                    ranked[i].Confidence,
                    new Evidence(EvidenceSource.Signal, $"{_neighbours} nearest sounds vote {ranked[i].Family} ({ranked[i].Confidence:0.00})")));
            }
        }

        double duration = sample.Technical?.DurationSeconds ?? 0;
        if (sample.Rhythm is { IsLoopLike: true })
        {
            proposals.Add(new ClassificationProposal(
                ClassificationAxis.ContentType,
                ContentType.Loop.ToString(),
                0.6,
                new Evidence(EvidenceSource.Signal, "periodic onsets and a whole number of beats")));
        }

        if (duration > 0 && duration < 1.2)
        {
            proposals.Add(new ClassificationProposal(
                ClassificationAxis.ContentType,
                ContentType.OneShot.ToString(),
                0.55,
                new Evidence(EvidenceSource.Signal, $"only {duration:0.00} s long")));
        }

        bool isLongAndDiffuse = duration >= 6.0 && sample.Spectral is { Flatness: >= 0.3 } && (sample.Rhythm?.OnsetsPerSecond ?? 0) < 1.0;
        if (isLongAndDiffuse)
        {
            proposals.Add(new ClassificationProposal(
                ClassificationAxis.ContentType,
                ContentType.Texture.ToString(),
                0.45,
                new Evidence(EvidenceSource.Signal, "long, noisy and without a beat")));
        }

        return proposals;
    }

    ///<summary>
    ///Family keys ranked by weighted neighbour vote, with the share of the vote as confidence. Empty when the sample has
    ///no fingerprint or there is nothing to compare against.
    ///</summary>
    public (string Family, double Confidence)[] Predict(Sample sample)
    {
        double[]? vector = _space.Vector(sample);
        if (vector is null || _training.Count == 0)
        {
            return [];
        }

        List<(double Distance, string Family)> nearest = [];
        foreach (TrainingExample example in _training)
        {
            bool isSelf = example.Id == sample.Id;
            if (!isSelf)
            {
                nearest.Add((FingerprintSpace.Distance(vector, example.Vector), example.FamilyKey));
            }
        }

        List<(double Distance, string Family)> top = [.. nearest.OrderBy(pair => pair.Distance).Take(_neighbours)];
        double totalWeight = 0;
        Dictionary<string, double> votes = [];
        foreach ((double distance, string family) in top)
        {
            double weight = 1.0 / (distance + DistanceSoftening);
            votes[family] = votes.GetValueOrDefault(family) + weight;
            totalWeight += weight;
        }

        double meanDistance = top.Count == 0 ? 0 : top.Average(pair => pair.Distance);
        double closeness = 0.5 + (0.5 * Math.Exp(-meanDistance / 4.0));
        return
        [
            .. votes
                .OrderByDescending(pair => pair.Value)
                .Select(pair => (pair.Key, Math.Min(ConfidenceCeiling, pair.Value / totalWeight * closeness))),
        ];
    }
}
