using System.Globalization;

namespace OvertonesPlayground.Ontology.Querying;

///<summary>
///Ready-made <see cref="SampleSpecification"/>s, one per question the ontology can answer.
///</summary>
public static class SampleSpecs
{
    #region Private methods
    private static bool IsInstrument(string instrumentKey, string ancestorKey) => Taxonomies.Instruments.TryGet(instrumentKey, out InstrumentConcept? concept) && concept.IsA(ancestorKey);

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static PredicateSpecification Of(string description, Func<Sample, bool> predicate) => new(description, predicate);
    #endregion

    #region Internal methods
    ///<summary>
    ///Lower-case text the free-text search runs against.
    ///</summary>
    internal static string SearchText(Sample sample)
    {
        List<string> parts = [sample.Name.ToLowerInvariant()];
        if (Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out InstrumentConcept? instrument))
        {
            parts.AddRange(instrument.AncestorsAndSelf().Select(concept => concept.DisplayName.ToLowerInvariant()));
        }

        if (sample.Classification.Kit is not null && Taxonomies.Kits.TryGet(sample.Classification.Kit.Value, out KitConcept? kit))
        {
            parts.Add(kit.DisplayName.ToLowerInvariant());
        }

        if (sample.Classification.Style is not null && Taxonomies.Styles.TryGet(sample.Classification.Style.Value, out StyleConcept? style))
        {
            parts.Add(style.DisplayName.ToLowerInvariant());
        }

        string? key = sample.Classification.Attributes.KeyName();
        if (key is not null)
        {
            parts.Add(key.ToLowerInvariant());
        }

        return string.Join(' ', parts);
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Matches everything.
    ///</summary>
    public static SampleSpecification Any() => Of("Any", _ => true);

    ///<summary>
    ///The sound is at least as bright as <paramref name="minHz"/> and at most <paramref name="maxHz"/> (spectral
    ///centroid).
    ///</summary>
    public static SampleSpecification CentroidBetween(double minHz, double maxHz) => Of($"Centroid {Number(minHz)}-{Number(maxHz)} Hz", s => s.Spectral is not null && s.Spectral.CentroidHz >= minHz && s.Spectral.CentroidHz <= maxHz);

    ///<summary>
    ///The content type is <paramref name="contentType"/>.
    ///</summary>
    public static SampleSpecification ContentTypeIs(ContentType contentType) => Of($"Content={contentType}", s => s.Classification.ContentType.Value == contentType);

    ///<summary>
    ///The sample lasts between <paramref name="minSeconds"/> and <paramref name="maxSeconds"/>.
    ///</summary>
    public static SampleSpecification DurationBetween(double minSeconds, double maxSeconds) => Of($"Duration {Number(minSeconds)}-{Number(maxSeconds)} s", s => s.Technical is not null && s.Technical.DurationSeconds >= minSeconds && s.Technical.DurationSeconds <= maxSeconds);

    ///<summary>
    ///The envelope has the given shape.
    ///</summary>
    public static SampleSpecification EnvelopeIs(EnvelopeShape shape) => Of($"Envelope={shape}", s => s.Dynamics is not null && s.Dynamics.Shape == shape);

    ///<summary>
    ///All of the given tonal descriptors apply.
    ///</summary>
    public static SampleSpecification HasCharacter(TonalCharacter character) => Of($"Character has {character}", s => s.Tonality is not null && (s.Tonality.Character & character) == character);

    ///<summary>
    ///The primary instrument (or, with <paramref name="includeAlternates"/>, any alternate) is the concept ///<paramref
    ///name="key"/> or one of its descendants.
    ///</summary>
    public static SampleSpecification InstrumentIs(string key, bool includeAlternates = false) => Of($"IsA({key})", s => IsInstrument(s.Classification.Instrument.Value, key) || (includeAlternates && s.Classification.AlternateInstruments.Any(alternate => IsInstrument(alternate.Value, key))));

    ///<summary>
    ///The kit is the concept <paramref name="key"/> or one of its descendants.
    ///</summary>
    public static SampleSpecification KitIs(string key) => Of($"Kit={key}", s => s.Classification.Kit is not null && Taxonomies.Kits.TryGet(s.Classification.Kit.Value, out KitConcept? kit) && kit.IsA(key));

    ///<summary>
    ///The sample belongs to the given length bucket.
    ///</summary>
    public static SampleSpecification Length(LengthClass length) => Of($"Length={length}", s => s.Technical is not null && s.Technical.Length == length);

    ///<summary>
    ///The sample belongs to the given loudness bucket.
    ///</summary>
    public static SampleSpecification Loudness(LoudnessClass loudness) => Of($"Loudness={loudness}", s => s.Dynamics is not null && s.Dynamics.Loudness == loudness);

    ///<summary>
    ///Integrated loudness lies between the two values.
    ///</summary>
    public static SampleSpecification LufsBetween(double minLufs, double maxLufs) => Of($"LUFS {Number(minLufs)}..{Number(maxLufs)}", s => s.Dynamics is not null && s.Dynamics.IntegratedLufs >= minLufs && s.Dynamics.IntegratedLufs <= maxLufs);

    ///<summary>
    ///The sample has a single channel or two identical channels.
    ///</summary>
    public static SampleSpecification Mono() => Of("Mono", s => s.Stereo is not null && s.Stereo.Image is StereoImage.Mono or StereoImage.DualMono);

    ///<summary>
    ///The name (or instrument, kit or style) contains every word of <paramref name="text"/>.
    ///</summary>
    public static SampleSpecification NameContains(string text)
    {
        string[] words = TextNormalizer.Tokenize(text);
        return Of($"Text '{text}'", s => words.All(word => SearchText(s).Contains(word, StringComparison.Ordinal)));
    }

    ///<summary>
    ///The classification is flagged for human review.
    ///</summary>
    public static SampleSpecification NeedsReview() => Of("NeedsReview", s => s.Classification.NeedsReview);

    ///<summary>
    ///The sound was analysed as originating from <paramref name="origin"/>.
    ///</summary>
    public static SampleSpecification OriginIs(SoundOrigin origin) => Of($"Origin={origin}", s => s.Classification.Origin.Value == origin);

    ///<summary>
    ///The effective pitch class (key in the name, else detected pitch) is <paramref name="pitchClass"/>.
    ///</summary>
    public static SampleSpecification PitchClassIs(int pitchClass) => Of($"Key={MusicalNotes.PitchClassName(pitchClass)}", s => s.EffectivePitchClass == pitchClass);

    ///<summary>
    ///The stereo image is <paramref name="image"/>.
    ///</summary>
    public static SampleSpecification StereoIs(StereoImage image) => Of($"Stereo={image}", s => s.Stereo is not null && s.Stereo.Image == image);

    ///<summary>
    ///The music style is the concept <paramref name="key"/> or one of its descendants.
    ///</summary>
    public static SampleSpecification StyleIs(string key) => Of($"Style={key}", s => s.Classification.Style is not null && Taxonomies.Styles.TryGet(s.Classification.Style.Value, out StyleConcept? style) && style.IsA(key));

    ///<summary>
    ///The effective tempo is at least <paramref name="fromBpm"/> and below <paramref name="belowBpm"/>, so neighbouring
    ///bands never share a sample (half and double time are not folded). Use <see cref="double.PositiveInfinity"/> for
    ///an open top band.
    ///</summary>
    public static SampleSpecification TempoBand(double fromBpm, double belowBpm) => Of($"Tempo {Number(fromBpm)}..<{Number(belowBpm)} BPM", s => s.EffectiveTempoBpm is { } bpm && bpm >= fromBpm && bpm < belowBpm);

    ///<summary>
    ///The effective tempo lies between the two values, both included (half and double time are not folded).
    ///</summary>
    public static SampleSpecification TempoBetween(double minBpm, double maxBpm) => Of($"Tempo {Number(minBpm)}-{Number(maxBpm)} BPM", s => s.EffectiveTempoBpm is { } bpm && bpm >= minBpm && bpm <= maxBpm);
    #endregion
}
