namespace OvertonesPlayground.Ontology.Querying;

///<summary>
///What a recommendation is for: the samples already in the project, the slot being filled and what the project is in.
///</summary>
public sealed record RecommendationContext
{
    ///<summary>Every sample the project already uses.</summary>
    public IReadOnlyList<Sample> InUse { get; init; } = [];

    ///<summary>The samples next to the slot (the same column or role), which a new sound should differ from.</summary>
    public IReadOnlyList<Sample> Neighbours { get; init; } = [];

    ///<summary>Instrument keys a sample must be (or descend from) to suit the slot; empty accepts any instrument.</summary>
    public IReadOnlyList<string> SlotInstruments { get; init; } = [];

    ///<summary>The project's key, if known; clashing samples are left out.</summary>
    public Key? Key { get; init; }

    ///<summary>The project's tempo, if known; loops at another tempo are left out.</summary>
    public double? TempoBpm { get; init; }

    ///<summary>The sample being swapped out, when looking for something like it; null when filling an empty slot.</summary>
    public Sample? Replacing { get; init; }

    ///<summary>Whether the slot wants loops (true), one-shots (false) or either (null).</summary>
    public bool? WantsLoop { get; init; }

    ///<summary>A kit key the sample must belong to (or descend from), or null for any kit.</summary>
    public string? Kit { get; init; }
}

///<summary>
///A recommended sample, its score and the reasons for it, in words.
///</summary>
public sealed record Recommendation(Sample Sample, double Score, IReadOnlyList<string> Reasons);

///<summary>
///How much each reason counts towards a recommendation's score.
///</summary>
public sealed record RecommendationWeights
{
    ///<summary>The sample plays notes of the project's key.</summary>
    public double KeyFit { get; init; } = 2.0;

    ///<summary>The loop runs at the project's tempo.</summary>
    public double Tempo { get; init; } = 2.0;

    ///<summary>The sample comes from a kit the project already uses (times the share of the project that kit makes up).</summary>
    public double SameKit { get; init; } = 2.0;

    ///<summary>The sample has the same origin (analog, acoustic, digital ...) as most of the project.</summary>
    public double SameOrigin { get; init; } = 0.5;

    ///<summary>The sample sounds like the one being replaced (times closeness, 0 to 1).</summary>
    public double Similar { get; init; } = 3.0;

    ///<summary>The classifier is sure what the sample is (times its confidence).</summary>
    public double Confidence { get; init; } = 1.0;

    ///<summary>The sample would pile sub-bass onto a bass the project already has.</summary>
    public double Masking { get; init; } = -2.0;

    ///<summary>The sample sounds nearly the same as one already next to the slot.</summary>
    public double NearDuplicate { get; init; } = -1.5;
}

///<summary>
///Recommends samples of the bank for a slot of a project, by rules that can be explained: the right instrument for the slot, in
///the project's key and at its tempo, from the kits and of the character the project already has, without muddying its low end,
///and not a near copy of what is already next to it. Every recommendation says why it was made.
///</summary>
public sealed class SampleRecommender
{
    #region Constants
    ///<summary>Share of a kick's energy below 60 Hz above which it crowds a bass.</summary>
    public const double MaxSubUnderBass = 0.35;

    ///<summary>Share of a sound's energy below 60 Hz above which it counts as a bass in the project.</summary>
    public const double BassSubShare = 0.5;

    ///<summary>Largest tempo mismatch, as a fraction, for a loop to count as at the project's tempo.</summary>
    public const double MaxTempoError = 0.005;

    ///<summary>Fingerprint distance under which two sounds are near copies of each other.</summary>
    public const double NearDuplicateDistance = 1.0;
    #endregion

    #region Fields
    private readonly SampleIndex _index;
    private readonly RecommendationWeights _weights;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates a recommender over the bank <paramref name="index"/>.
    ///</summary>
    public SampleRecommender(SampleIndex index, RecommendationWeights? weights = null)
    {
        _index = index;
        _weights = weights ?? new RecommendationWeights();
    }
    #endregion

    #region Private methods
    private static bool IsInstrument(Sample sample, IReadOnlyList<string> keys) =>
        keys.Count == 0 || (Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out InstrumentConcept? concept) && keys.Any(concept.IsA));

    private static bool IsInKit(Sample sample, string kit) =>
        sample.Classification.Kit is { } label && Taxonomies.Kits.TryGet(label.Value, out KitConcept? concept) && concept.IsA(kit);

    private static bool IsLoop(Sample sample) => sample.Classification.ContentType.Value is ContentType.Loop or ContentType.Break or ContentType.Phrase;

    private static bool RunsAt(Sample sample, double bpm) => sample.Classification.Attributes.TempoBpm is { } tempo && Math.Abs(tempo - bpm) / bpm <= MaxTempoError;

    private double Distance(Sample a, Sample b)
    {
        double[]? va = _index.Space.Vector(a);
        double[]? vb = _index.Space.Vector(b);
        return va is null || vb is null ? double.PositiveInfinity : FingerprintSpace.Distance(va, vb);
    }

    private Recommendation? Score(Sample candidate, RecommendationContext context, IReadOnlyDictionary<string, int> kits, SoundOrigin? origin, bool hasBass)
    {
        bool isInUse = context.InUse.Any(sample => sample.Id == candidate.Id);
        bool isUnsuitable = isInUse
            || candidate.Status != AnalysisStatus.Analyzed
            || !IsInstrument(candidate, context.SlotInstruments)
            || (context.WantsLoop is { } wantsLoop && IsLoop(candidate) != wantsLoop)
            || (context.Kit is { } requiredKit && !IsInKit(candidate, requiredKit));
        if (isUnsuitable)
        {
            return null;
        }

        List<string> reasons = [];
        double score = _weights.Confidence * candidate.Classification.Instrument.Confidence;

        if (context.Key is { } key)
        {
            switch (KeyCompatibility.Judge(candidate, key))
            {
                case KeyFit.Clashes:
                    return null;
                case KeyFit.Fits:
                    score += _weights.KeyFit;
                    reasons.Add($"in {key.Name}");
                    break;
                case KeyFit.Neutral:
                default:
                    break;
            }
        }

        if (IsLoop(candidate) && context.TempoBpm is { } bpm)
        {
            if (!RunsAt(candidate, bpm))
            {
                return null;
            }

            score += _weights.Tempo;
            reasons.Add($"at {bpm:0} BPM");
        }

        int total = kits.Values.Sum();
        if (candidate.Classification.Kit?.Value is { } kit && kits.TryGetValue(kit, out int count) && total > 0)
        {
            score += _weights.SameKit * (0.5 + (0.5 * count / total));
            reasons.Add($"same kit ({kit})");
        }

        if (origin is { } o && candidate.Classification.Origin.Value == o)
        {
            score += _weights.SameOrigin;
        }

        bool isBoomy = candidate.Spectral is { } spectral && spectral.Bands.Sub > MaxSubUnderBass;
        if (hasBass && isBoomy && IsInstrument(candidate, ["kick"]))
        {
            score += _weights.Masking;
            reasons.Add("heavy sub-bass (may crowd the bass)");
        }

        if (context.Replacing is { } replacing)
        {
            double closeness = 1.0 / (1.0 + Distance(candidate, replacing));
            score += _weights.Similar * closeness;
            if (closeness >= 0.4)
            {
                reasons.Add($"sounds like {replacing.Name}");
            }
        }

        Sample? twin = context.Neighbours.FirstOrDefault(neighbour => neighbour.Id != context.Replacing?.Id && Distance(candidate, neighbour) < NearDuplicateDistance);
        if (twin is not null)
        {
            score += _weights.NearDuplicate;
            reasons.Add($"close to {twin.Name}");
        }

        if (reasons.Count == 0)
        {
            reasons.Add(Taxonomies.Instruments.TryGet(candidate.Classification.Instrument.Value, out InstrumentConcept? concept) ? concept.DisplayName.ToLowerInvariant() : candidate.Classification.Instrument.Value);
        }

        return new Recommendation(candidate, Math.Round(score, 4), reasons);
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Up to <paramref name="count"/> samples for the slot described by <paramref name="context"/>, best first. Samples that clash
    ///with the key, loops at another tempo, samples of the wrong instrument and samples already in the project are left out; of two
    ///near copies only the better is kept, so the list offers a real choice.
    ///</summary>
    public IReadOnlyList<Recommendation> Recommend(RecommendationContext context, int count)
    {
        Dictionary<string, int> kits = context.InUse
            .Select(sample => sample.Classification.Kit?.Value)
            .OfType<string>()
            .GroupBy(kit => kit, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        SoundOrigin? origin = context.InUse.Count == 0 ? null : context.InUse.GroupBy(sample => sample.Classification.Origin.Value).OrderByDescending(group => group.Count()).First().Key;
        bool hasBass = context.InUse.Any(sample => sample.Spectral is { } spectral && spectral.Bands.Sub >= BassSubShare && IsInstrument(sample, ["bass"]));

        List<Recommendation> ranked =
        [
            .. _index.All
                .Select(candidate => Score(candidate, context, kits, origin, hasBass))
                .OfType<Recommendation>()
                .OrderByDescending(recommendation => recommendation.Score)
                .ThenBy(recommendation => recommendation.Sample.Id, StringComparer.Ordinal),
        ];

        List<Recommendation> chosen = [];
        foreach (Recommendation recommendation in ranked)
        {
            if (chosen.Count >= count)
            {
                break;
            }

            bool isNearCopy = chosen.Any(other => Distance(other.Sample, recommendation.Sample) < NearDuplicateDistance / 2);
            if (!isNearCopy)
            {
                chosen.Add(recommendation);
            }
        }

        return chosen;
    }

    ///<summary>
    ///The major or minor key that best explains the pitched samples of a project: each named chord or note votes for the keys that
    ///contain it, and a tonic that is itself played wins ties. Null when nothing in the project has a known pitch.
    ///</summary>
    public static Key? InferKey(IEnumerable<Sample> samples)
    {
        List<(PitchClass Note, double Weight)> votes = [];
        foreach (Sample sample in samples)
        {
            if (!KeyCompatibility.IsPassage(sample) && Chord.TryFindInName(sample.Name, out Chord chord))
            {
                votes.AddRange(chord.Triad.PitchClasses.Select(note => (note, 1.0)));
            }
            else if (KeyCompatibility.PitchOf(sample) is { } pitch)
            {
                votes.Add((pitch, 1.0));
                if (sample.Classification.Attributes.KeyMode is not KeyMode.None)
                {
                    PitchClass third = pitch.Transpose(sample.Classification.Attributes.KeyMode == KeyMode.Minor ? Interval.MinorThird : Interval.MajorThird);
                    votes.Add((third, 0.5));
                    votes.Add((pitch.Transpose(Interval.PerfectFifth), 0.5));
                }
            }
        }

        if (votes.Count == 0)
        {
            return null;
        }

        IEnumerable<Key> keys = Enumerable.Range(0, 12).SelectMany(root => new[] { Key.MinorOf(new PitchClass(root)), Key.MajorOf(new PitchClass(root)) });
        return keys
            .Select(key => (Key: key, Score: votes.Where(vote => key.Contains(vote.Note)).Sum(vote => vote.Weight) + (0.01 * votes.Where(vote => vote.Note == key.Tonic).Sum(vote => vote.Weight))))
            .OrderByDescending(pair => pair.Score)
            .ThenBy(pair => pair.Key.IsMinor ? 0 : 1)
            .ThenBy(pair => pair.Key.Tonic.Value)
            .First().Key;
    }

    ///<summary>
    ///The tempo of a project's loops (the most common one), or null when it has none with a tempo in its name.
    ///</summary>
    public static double? InferTempo(IEnumerable<Sample> samples) =>
        samples.Where(IsLoop)
            .Select(sample => sample.Classification.Attributes.TempoBpm)
            .OfType<double>()
            .GroupBy(bpm => Math.Round(bpm))
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => (double?)group.Key)
            .FirstOrDefault();
    #endregion
}
