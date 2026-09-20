using System.Globalization;

namespace OvertonesPlayground.Ontology.Querying;

///<summary>
///In-memory index over a catalog: lookup by id, free-text and specification queries, similarity search, collections
///(kits, variations, tempo and key groups, sound families) and per-facet counts for building filter UIs. Built once, then
///read-only.
///</summary>
public sealed class SampleIndex
{
    #region Constants
    private const double TempoTolerance = 0.04;
    #endregion

    #region Fields
    private readonly Dictionary<string, Sample> _byId;
    private readonly Dictionary<string, string> _searchText;
    private readonly Dictionary<string, List<Sample>> _byStem = new(StringComparer.Ordinal);
    private readonly Lazy<IReadOnlyList<Kit>> _kits;
    private readonly Lazy<(IReadOnlyList<SoundFamily> Families, Dictionary<string, SoundFamily> BySample)> _families;
    #endregion

    #region Constructors
    ///<summary>Indexes <paramref name="samples"/>.</summary>
    public SampleIndex(IEnumerable<Sample> samples)
    {
        All = [.. samples.OrderBy(sample => sample.Name, StringComparer.OrdinalIgnoreCase)];
        _byId = All.ToDictionary(sample => sample.Id, StringComparer.Ordinal);
        _searchText = All.ToDictionary(sample => sample.Id, SampleSpecs.SearchText, StringComparer.Ordinal);
        Space = new FingerprintSpace(All);

        foreach (Sample sample in All)
        {
            string stem = sample.Classification.Attributes.Stem;
            bool hasStem = !string.IsNullOrWhiteSpace(stem);
            if (!hasStem)
            {
                continue;
            }

            if (!_byStem.TryGetValue(stem, out List<Sample>? group))
            {
                group = [];
                _byStem[stem] = group;
            }

            group.Add(sample);
        }

        _kits = new Lazy<IReadOnlyList<Kit>>(BuildKits);
        _families = new Lazy<(IReadOnlyList<SoundFamily>, Dictionary<string, SoundFamily>)>(BuildFamilies);
    }
    #endregion

    #region Private methods
    private IReadOnlyList<Kit> BuildKits()
    {
        Dictionary<string, List<Sample>> members = new(StringComparer.Ordinal);
        foreach (Sample sample in All)
        {
            Label<string>? label = sample.Classification.Kit;
            if (label is null)
            {
                continue;
            }

            if (!members.TryGetValue(label.Value, out List<Sample>? list))
            {
                list = [];
                members[label.Value] = list;
            }

            list.Add(sample);
        }

        return
        [
            .. members
                .Where(pair => Taxonomies.Kits.TryGet(pair.Key, out _))
                .Select(pair => new Kit(Taxonomies.Kits.Get(pair.Key), pair.Value))
                .OrderByDescending(kit => kit.Members.Count)
                .ThenBy(kit => kit.Name, StringComparer.OrdinalIgnoreCase),
        ];
    }

    private (IReadOnlyList<SoundFamily> Families, Dictionary<string, SoundFamily> BySample) BuildFamilies()
    {
        IReadOnlyList<SoundFamily> families = SoundFamilyClusterer.Cluster(All, Space);
        Dictionary<string, SoundFamily> bySample = new(StringComparer.Ordinal);
        foreach (SoundFamily family in families)
        {
            foreach (Sample member in family.Members)
            {
                bySample[member.Id] = family;
            }
        }

        return (families, bySample);
    }

    ///<summary>Tempo ratio folded to half / double time: 60 and 120 count as compatible.</summary>
    private static bool TemposMatch(double a, double b)
    {
        bool isInvalid = a <= 0 || b <= 0;
        if (isInvalid)
        {
            return false;
        }

        foreach (double factor in (ReadOnlySpan<double>)[1.0, 2.0, 0.5])
        {
            if (Math.Abs((a * factor) - b) / b <= TempoTolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static double SortValue(Sample sample, SampleSortKey key) =>
        key switch
        {
            SampleSortKey.Duration => sample.Technical?.DurationSeconds ?? 0,
            SampleSortKey.Loudness => sample.Dynamics?.IntegratedLufs ?? -200,
            SampleSortKey.Brightness => sample.Spectral?.CentroidHz ?? 0,
            SampleSortKey.Attack => sample.Dynamics?.AttackMs ?? 0,
            SampleSortKey.Tempo => sample.EffectiveTempoBpm ?? 0,
            SampleSortKey.Pitch => sample.Tonality?.MidiNote ?? (sample.EffectivePitchClass ?? 0),
            _ => 0,
        };
    #endregion

    #region Public methods
    ///<summary>Number of samples per value of <paramref name="selector"/>; samples where it returns null are skipped.</summary>
    public IReadOnlyDictionary<TKey, int> CountBy<TKey>(Func<Sample, TKey?> selector)
        where TKey : struct
    {
        Dictionary<TKey, int> counts = [];
        foreach (Sample sample in All)
        {
            TKey? key = selector(sample);
            if (key is { } value)
            {
                counts[value] = counts.GetValueOrDefault(value) + 1;
            }
        }

        return counts;
    }

    ///<summary>Number of samples per text value (for example a kit or style key); samples where it returns null are skipped.</summary>
    public IReadOnlyDictionary<string, int> CountByText(Func<Sample, string?> selector)
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        foreach (Sample sample in All)
        {
            string? key = selector(sample);
            if (key is not null)
            {
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }

        return counts;
    }

    ///<summary>
    ///Number of samples per instrument concept key. A sample counts toward its own concept and every ancestor, so the
    ///count of <c>percussion</c> includes all kicks, snares and hats.
    ///</summary>
    public IReadOnlyDictionary<string, int> CountByInstrument()
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        foreach (Sample sample in All)
        {
            if (Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out InstrumentConcept? concept))
            {
                foreach (OntologyConcept ancestor in concept.AncestorsAndSelf())
                {
                    counts[ancestor.Key] = counts.GetValueOrDefault(ancestor.Key) + 1;
                }
            }
        }

        return counts;
    }

    ///<summary>The sample with the given asset file name, or null.</summary>
    public Sample? Find(string id) => _byId.GetValueOrDefault(id);

    ///<summary>The <paramref name="count"/> samples nearest to <paramref name="sample"/> in acoustic feature space.</summary>
    public IReadOnlyList<(Sample Sample, double Distance)> FindSimilar(Sample sample, int count)
    {
        double[]? origin = Space.Vector(sample);
        if (origin is null)
        {
            return [];
        }

        List<(Sample Sample, double Distance)> scored = [];
        foreach (Sample other in All)
        {
            double[]? vector = ReferenceEquals(other, sample) || other.Id == sample.Id ? null : Space.Vector(other);
            if (vector is not null)
            {
                scored.Add((other, FingerprintSpace.Distance(origin, vector)));
            }
        }

        return [.. scored.OrderBy(pair => pair.Distance).ThenBy(pair => pair.Sample.Name, StringComparer.OrdinalIgnoreCase).Take(count)];
    }

    ///<summary>All samples that are, or descend from, the kit concept <paramref name="kitKey"/>.</summary>
    public IReadOnlyList<Sample> GetKitMembers(string kitKey) => [.. All.Where(SampleSpecs.KitIs(kitKey).IsSatisfiedBy)];

    ///<summary>The kits present in the corpus, largest first.</summary>
    public IReadOnlyList<Kit> GetKits() => _kits.Value;

    ///<summary>
    ///The sound families of the corpus, largest first: clusters of sounds that sound alike, found from the audio alone. They are
    ///computed the first time they are asked for and are always the same for the same catalog.
    ///</summary>
    public IReadOnlyList<SoundFamily> GetSoundFamilies() => _families.Value.Families;

    ///<summary>The family <paramref name="sample"/> belongs to, or null when it has no acoustic analysis to cluster on.</summary>
    public SoundFamily? GetSoundFamily(Sample sample) => _families.Value.BySample.GetValueOrDefault(sample.Id);

    ///<summary>Samples that share the effective pitch class <paramref name="pitchClass"/>.</summary>
    public KeyGroup GetKeyGroup(int pitchClass) => new(pitchClass, [.. All.Where(sample => sample.EffectivePitchClass == pitchClass)]);

    ///<summary>Samples whose tempo matches <paramref name="bpm"/> within 4 %, allowing half and double time.</summary>
    public TempoGroup GetTempoGroup(double bpm) =>
        new(bpm, [.. All.Where(sample => sample.EffectiveTempoBpm is { } other && TemposMatch(other, bpm))]);

    ///<summary>The other numbered variations of <paramref name="sample"/> (same name stem), or an empty set.</summary>
    public VariationSet GetVariations(Sample sample)
    {
        string stem = sample.Classification.Attributes.Stem;
        List<Sample> group = _byStem.GetValueOrDefault(stem) ?? [];
        return new VariationSet(stem, [.. group.OrderBy(member => member.Classification.Attributes.VariationNumber ?? 0).ThenBy(member => member.Name, StringComparer.OrdinalIgnoreCase)]);
    }

    ///<summary>
    ///Typed relations from <paramref name="sample"/> to others, at most <paramref name="perType"/> of each type.
    ///</summary>
    public IReadOnlyList<SampleRelationship> GetRelationships(Sample sample, int perType = 6)
    {
        List<SampleRelationship> relations = [];
        HashSet<string> used = new(StringComparer.Ordinal) { sample.Id };

        foreach (Sample variation in GetVariations(sample).Members.Where(member => member.Id != sample.Id).Take(perType))
        {
            relations.Add(new SampleRelationship(SampleRelationType.VariationOf, variation, 1.0));
            _ = used.Add(variation.Id);
        }

        Label<string>? kit = sample.Classification.Kit;
        if (kit is not null)
        {
            InstrumentConcept? family = Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out InstrumentConcept? own) ? own.Family : null;
            IEnumerable<Sample> sameKit = All.Where(other => other.Classification.Kit?.Value == kit.Value && !used.Contains(other.Id));
            List<Sample> sameKitList = [.. sameKit];

            foreach (Sample complement in sameKitList
                .Where(other => IsKitPiece(other) && IsKitPiece(sample) && FamilyOf(other) != family)
                .GroupBy(FamilyOf)
                .Select(group => group.First())
                .Take(perType))
            {
                relations.Add(new SampleRelationship(SampleRelationType.Complements, complement, 0.7));
                _ = used.Add(complement.Id);
            }

            foreach (Sample other in sameKitList.Where(other => !used.Contains(other.Id)).Take(perType))
            {
                relations.Add(new SampleRelationship(SampleRelationType.SameKit, other, 0.5));
                _ = used.Add(other.Id);
            }
        }

        double? tempo = sample.EffectiveTempoBpm;
        if (tempo is not null)
        {
            foreach (Sample other in All.Where(other => other.Id != sample.Id && other.EffectiveTempoBpm is { } t && TemposMatch(t, tempo.Value)).Take(perType))
            {
                relations.Add(new SampleRelationship(SampleRelationType.TempoCompatible, other, 0.6));
            }
        }

        int? pitchClass = sample.EffectivePitchClass;
        bool isPitched = sample.Tonality is { PitchConfidence: >= 0.8 } || sample.Classification.Attributes.KeyPitchClass is not null;
        if (pitchClass is not null && isPitched)
        {
            foreach (Sample other in All.Where(other => other.Id != sample.Id && other.EffectivePitchClass == pitchClass && (other.Tonality is { PitchConfidence: >= 0.8 } || other.Classification.Attributes.KeyPitchClass is not null)).Take(perType))
            {
                relations.Add(new SampleRelationship(SampleRelationType.PitchCompatible, other, 0.6));
            }
        }

        foreach ((Sample similar, double distance) in FindSimilar(sample, perType))
        {
            relations.Add(new SampleRelationship(SampleRelationType.SimilarTo, similar, 1.0 / (1.0 + distance)));
        }

        return relations;
    }

    ///<summary>Runs <paramref name="query"/>.</summary>
    public IReadOnlyList<Sample> Query(SampleQuery query)
    {
        IEnumerable<Sample> results = All;

        string[] words = string.IsNullOrWhiteSpace(query.Text) ? [] : TextNormalizer.Tokenize(query.Text);
        if (words.Length > 0)
        {
            results = results.Where(sample => words.All(word => _searchText[sample.Id].Contains(word, StringComparison.Ordinal)));
        }

        if (query.Filter is not null)
        {
            results = results.Where(query.Filter.IsSatisfiedBy);
        }

        if (query.SimilarTo is not null)
        {
            double[]? origin = Space.Vector(query.SimilarTo);
            results = origin is null
                ? []
                : results
                    .Where(sample => sample.Id != query.SimilarTo.Id)
                    .Select(sample => (Sample: sample, Vector: Space.Vector(sample)))
                    .Where(pair => pair.Vector is not null)
                    .OrderBy(pair => FingerprintSpace.Distance(origin, pair.Vector!))
                    .Select(pair => pair.Sample);
        }
        else
        {
            results = Order(results, query.Sort, query.Descending);
        }

        return query.Limit is { } limit ? [.. results.Take(limit)] : [.. results];
    }
    #endregion

    #region Private helpers
    private static InstrumentConcept? FamilyOf(Sample sample) =>
        Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out InstrumentConcept? concept) ? concept.Family : null;

    private static bool IsKitPiece(Sample sample) =>
        Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out InstrumentConcept? concept) && concept.IsA("percussion");

    private static IEnumerable<Sample> Order(IEnumerable<Sample> samples, SampleSortKey key, bool descending)
    {
        if (key == SampleSortKey.Name)
        {
            return descending
                ? samples.OrderByDescending(sample => sample.Name, StringComparer.OrdinalIgnoreCase)
                : samples.OrderBy(sample => sample.Name, StringComparer.OrdinalIgnoreCase);
        }

        if (key == SampleSortKey.Instrument)
        {
            static string Path(Sample sample) =>
                Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out InstrumentConcept? concept)
                    ? concept.Path
                    : sample.Classification.Instrument.Value;

            return descending
                ? samples.OrderByDescending(Path, StringComparer.OrdinalIgnoreCase).ThenBy(sample => sample.Name, StringComparer.OrdinalIgnoreCase)
                : samples.OrderBy(Path, StringComparer.OrdinalIgnoreCase).ThenBy(sample => sample.Name, StringComparer.OrdinalIgnoreCase);
        }

        return descending
            ? samples.OrderByDescending(sample => SortValue(sample, key)).ThenBy(sample => sample.Name, StringComparer.OrdinalIgnoreCase)
            : samples.OrderBy(sample => SortValue(sample, key)).ThenBy(sample => sample.Name, StringComparer.OrdinalIgnoreCase);
    }
    #endregion

    #region Public properties
    ///<summary>Every sample, ordered by name.</summary>
    public IReadOnlyList<Sample> All { get; }

    ///<summary>Number of samples.</summary>
    public int Count => All.Count;

    ///<summary>The standardized feature space used for similarity.</summary>
    public FingerprintSpace Space { get; }
    #endregion
}
