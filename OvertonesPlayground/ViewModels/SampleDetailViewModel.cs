using System.Globalization;
using OvertonesPlayground.Ontology.Concepts;
using OvertonesPlayground.Ontology.Facets;
using OvertonesPlayground.Ontology.Model;
using OvertonesPlayground.Ontology.Querying;

namespace OvertonesPlayground.ViewModels;

///<summary>
///One labelled fact in the detail panel, for example <c>Loudness</c> / <c>-14.8 LUFS (Loud)</c>.
///</summary>
///<param name="Label">What the fact is.</param>
///<param name="Value">Its value, already formatted.</param>
public sealed record FactViewModel(string Label, string Value);

///<summary>
///One bar of the spectral-balance graph in the detail panel.
///</summary>
///<param name="Band">Name of the frequency band with its range.</param>
///<param name="Fraction">Share of the sound's energy in the band, 0 - 1.</param>
public sealed record BandBarViewModel(string Band, double Fraction)
{
    #region Public properties
    ///<summary>
    ///The share as a percentage, for display.
    ///</summary>
    public string Percent => (Fraction * 100).ToString("0.#", CultureInfo.InvariantCulture) + " %";
    #endregion
}

///<summary>
///A sound related to the selected one, with the reason it is shown.
///</summary>
///<param name="Sample">The related sound.</param>
///<param name="Reason">How it relates, for example "same kit".</param>
public sealed record RelatedSampleViewModel(Sample Sample, string Reason)
{
    #region Public properties
    ///<summary>
    ///The related sound's name.
    ///</summary>
    public string Name => Sample.Name;
    #endregion
}

///<summary>
///A titled list of related sounds ("Variations", "Sounds like this" ...).
///</summary>
///<param name="Title">Heading.</param>
///<param name="Items">The related sounds.</param>
public sealed record RelatedGroupViewModel(string Title, IReadOnlyList<RelatedSampleViewModel> Items);

///<summary>
///Everything the Sound Bank detail panel shows about the selected sound: the classification and its evidence, the measured
///facts of each acoustic category, the spectral balance, and the sounds related to it.
///</summary>
public sealed class SampleDetailViewModel
{
    #region Constants
    private const int RelatedPerGroup = 6;
    #endregion

    #region Constructors
    ///<summary>
    ///Builds the detail for <paramref name="sample"/>, finding its relations in <paramref name="index"/>.
    ///</summary>
    public SampleDetailViewModel(Sample sample, SampleIndex index)
    {
        Sample = sample;
        Facts = BuildFacts(sample);
        Bands = BuildBands(sample);
        Related = BuildRelated(sample, index);
        ReviewNotes = sample.Classification.NeedsReview
            ? [.. sample.Classification.ReviewReasons]
            : [];
        Evidence = [.. sample.Classification.Instrument.Evidence.Select(evidence => $"{evidence.Source}: {evidence.Detail}")];
    }
    #endregion

    #region Private methods
    private static string Number(double value, string format = "0.#") => value.ToString(format, CultureInfo.InvariantCulture);

    ///<summary>
    ///Turns <c>OutOfPhase</c> into <c>Out Of Phase</c>.
    ///</summary>
    internal static string Words(string pascalCase) => System.Text.RegularExpressions.Regex.Replace(pascalCase, "(?<=[a-z])(?=[A-Z])", " ");

    private static List<FactViewModel> BuildFacts(Sample sample)
    {
        List<FactViewModel> facts = [];
        SampleClassification classification = sample.Classification;

        bool isKnown = Taxonomies.Instruments.TryGet(classification.Instrument.Value, out InstrumentConcept? instrument) && classification.Instrument.Value != "unclassified";
        string confidence = $"{Number(classification.Instrument.Confidence * 100, "0")} % sure";
        facts.Add(new FactViewModel("Instrument", isKnown ? $"{instrument!.Path}  ({confidence})" : "Unclassified"));
        if (classification.AlternateInstruments.Count > 0)
        {
            IEnumerable<string> others = classification.AlternateInstruments
                .Select(label => Taxonomies.Instruments.TryGet(label.Value, out InstrumentConcept? alt) ? alt.DisplayName : label.Value);
            facts.Add(new FactViewModel("Could also be", string.Join(", ", others)));
        }

        if (classification.Kit is not null && Taxonomies.Kits.TryGet(classification.Kit.Value, out KitConcept? kit))
        {
            facts.Add(new FactViewModel("Kit", kit.Path));
        }

        if (classification.Style is not null && Taxonomies.Styles.TryGet(classification.Style.Value, out StyleConcept? style))
        {
            facts.Add(new FactViewModel("Style", style.Path));
        }

        List<string> kinds = [];
        if (classification.ContentType.Value != ContentType.Unknown)
        {
            kinds.Add(Words(classification.ContentType.Value.ToString()));
        }

        if (classification.Origin.Value != SoundOrigin.Unknown)
        {
            kinds.Add(Words(classification.Origin.Value.ToString()));
        }

        if (kinds.Count > 0)
        {
            facts.Add(new FactViewModel("Type", string.Join("  ·  ", kinds)));
        }

        if (sample.Technical is { } technical)
        {
            string channels = technical.Channels == 1 ? "mono" : technical.Channels == 2 ? "stereo" : $"{technical.Channels} channels";
            facts.Add(new FactViewModel("File", $"{technical.BitsPerSample}-bit {Number(technical.SampleRate / 1000.0, "0.###")} kHz {channels}  ·  {Number(technical.FileSizeBytes / 1024.0, "N0")} KB"));
            facts.Add(new FactViewModel("Length", $"{SampleRowViewModel.FormatDuration(technical.DurationSeconds)}  ({technical.Length})"));
        }

        if (sample.Dynamics is { } dynamics)
        {
            facts.Add(new FactViewModel("Loudness", $"{Number(dynamics.IntegratedLufs)} LUFS  ({dynamics.Loudness})  ·  peak {Number(dynamics.PeakDb)} dB  ·  crest {Number(dynamics.CrestDb)} dB"));
            facts.Add(new FactViewModel("Envelope", $"{dynamics.Shape}  ·  attack {Number(dynamics.AttackMs, "0")} ms  ·  decay {Number(dynamics.DecayMs, "0")} ms"));
            if (dynamics.ClippedSamples > 0 || Math.Abs(dynamics.DcOffset) > 0.01)
            {
                facts.Add(new FactViewModel("Watch out", $"{(dynamics.ClippedSamples > 0 ? $"{dynamics.ClippedSamples:N0} clipped samples" : string.Empty)}{(Math.Abs(dynamics.DcOffset) > 0.01 ? $"  DC offset {Number(dynamics.DcOffset * 100)} %" : string.Empty)}".Trim()));
            }
        }

        if (sample.Spectral is { } spectral)
        {
            facts.Add(new FactViewModel("Brightness", $"centroid {Number(spectral.CentroidHz, "N0")} Hz  ·  rolloff {Number(spectral.RolloffHz, "N0")} Hz  ·  flatness {Number(spectral.Flatness, "0.00")}"));
        }

        if (sample.Tonality is { } tonality)
        {
            string pitch = tonality.FundamentalHz is null
                ? "no stable pitch"
                : $"{tonality.NoteName}  ·  {Number(tonality.FundamentalHz.Value)} Hz  ·  {(tonality.CentsOffset >= 0 ? "+" : string.Empty)}{Number(tonality.CentsOffset ?? 0, "0")} cents";
            facts.Add(new FactViewModel("Pitch", pitch));
            facts.Add(new FactViewModel("Character", tonality.Character == TonalCharacter.None ? "-" : tonality.Character.ToString().Replace(",", " ·", StringComparison.Ordinal)));
        }

        if (sample.Stereo is { } stereo)
        {
            facts.Add(new FactViewModel("Stereo", stereo.Channels == 1
                ? "Mono"
                : $"{Words(stereo.Image.ToString())}  ·  correlation {Number(stereo.Correlation, "0.00")}  ·  mono sum {Number(stereo.MonoCompatibilityDb)} dB"));
        }

        string? key = classification.Attributes.KeyName();
        if (sample.EffectiveTempoBpm is not null || key is not null)
        {
            List<string> musical = [];
            if (sample.EffectiveTempoBpm is { } tempo)
            {
                string source = classification.Attributes.TempoBpm is not null ? "from the name" : "detected";
                musical.Add($"{Number(tempo, "0")} BPM ({source})");
            }

            if (key is not null)
            {
                musical.Add($"key {key}");
            }

            if (sample.Rhythm is { IsLoopLike: true, Bars: { } bars })
            {
                musical.Add($"{Number(bars, "0.#")} bars, loops cleanly");
            }

            facts.Add(new FactViewModel("Tempo / key", string.Join("  ·  ", musical)));
        }

        return facts;
    }

    private static List<BandBarViewModel> BuildBands(Sample sample)
    {
        if (sample.Spectral is null)
        {
            return [];
        }

        string[] names = ["Sub  < 60 Hz", "Bass  60 - 250", "Low-mid  250 - 500", "Mid  0.5 - 2 k", "High-mid  2 - 4 k", "Presence  4 - 8 k", "Air  > 8 k"];
        double[] fractions = sample.Spectral.Bands.ToArray();
        return [.. names.Select((name, i) => new BandBarViewModel(name, Math.Clamp(fractions[i], 0, 1)))];
    }

    private static List<RelatedGroupViewModel> BuildRelated(Sample sample, SampleIndex index)
    {
        IReadOnlyList<SampleRelationship> relationships = index.GetRelationships(sample, RelatedPerGroup);
        List<RelatedGroupViewModel> groups = [];
        (SampleRelationType Type, string Title, string Reason)[] order =
        [
            (SampleRelationType.VariationOf, "Variations", "variation"),
            (SampleRelationType.SameKit, "From the same kit", "same kit"),
            (SampleRelationType.Complements, "Goes with (same kit)", "complements"),
            (SampleRelationType.SimilarTo, "Sounds like this", "similar sound"),
            (SampleRelationType.TempoCompatible, "Same tempo", "tempo"),
            (SampleRelationType.PitchCompatible, "Same key", "key"),
        ];

        foreach ((SampleRelationType type, string title, string reason) in order)
        {
            List<RelatedSampleViewModel> items = [.. relationships.Where(r => r.Type == type).Select(r => new RelatedSampleViewModel(r.Target, reason))];
            if (items.Count > 0)
            {
                groups.Add(new RelatedGroupViewModel(title, items));
            }
        }

        return groups;
    }
    #endregion

    #region Public properties
    ///<summary>
    ///The bars of the spectral-balance graph, lowest band first.
    ///</summary>
    public IReadOnlyList<BandBarViewModel> Bands { get; }

    ///<summary>
    ///Why the instrument label was given, strongest evidence first.
    ///</summary>
    public IReadOnlyList<string> Evidence { get; }

    ///<summary>
    ///The labelled facts, from classification down through each acoustic category.
    ///</summary>
    public IReadOnlyList<FactViewModel> Facts { get; }

    ///<summary>
    ///Whether the classification needs a maintainer's attention.
    ///</summary>
    public bool NeedsReview => ReviewNotes.Count > 0;

    ///<summary>
    ///Sounds related to this one, grouped by how.
    ///</summary>
    public IReadOnlyList<RelatedGroupViewModel> Related { get; }

    ///<summary>
    ///The sound described.
    ///</summary>
    public Sample Sample { get; }

    ///<summary>
    ///Why the classification is flagged for review, if it is.
    ///</summary>
    public IReadOnlyList<string> ReviewNotes { get; }

    ///<summary>
    ///The sound's name.
    ///</summary>
    public string Title => Sample.Name;
    #endregion
}
