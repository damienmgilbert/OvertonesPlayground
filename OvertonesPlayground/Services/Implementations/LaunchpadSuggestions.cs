using System.Text.RegularExpressions;
using OvertonesPlayground.Models;
using OvertonesPlayground.Ontology.Concepts;
using OvertonesPlayground.Ontology.Model;
using OvertonesPlayground.Ontology.Querying;
using OvertonesPlayground.Ontology.Theory;
using static OvertonesPlayground.Services.Implementations.LaunchpadExampleBuilder;

namespace OvertonesPlayground.Services.Implementations;

///<summary>
///Suggests Sound Bank sounds for the pads of a Launchpad project. It reads the project the way a musician would (which sounds
///are on it, what each column is for, what key and tempo it is in) and hands that to the ontology's
///<see cref="SampleRecommender"/>, which explains each suggestion.
///</summary>
///<remarks>
///What a pad's column is for comes from the sounds already in that column of the bank (a column of snares wants snares); an empty
///column falls back to the lane roles the examples use (see <see cref="LaunchpadExampleBuilder"/>). The key is the project's own
///if it has one, otherwise the key its pitched sounds point to; the tempo is always the project's, because loops must run at it.
///</remarks>
internal static partial class LaunchpadSuggestions
{
    #region Fields
    private static readonly Dictionary<int, string[]> _laneInstruments = new()
    {
        [Low] = ["kick", "bass"],
        [Backbeat] = ["snare", "clap", "drum-loop", "mixed-loop"],
        [Accent] = ["clap", "rim", "snap", "vocal"],
        [Hats] = ["hihat"],
        [Cymbals] = ["cymbal"],
        [Percussion] = ["hand-percussion", "electronic-percussion", "tuned-percussion"],
        [Toms] = ["tom", "atmosphere", "noise"],
        [Colour] = ["bass", "keys", "synth", "stab", "strings-plucked", "brass-winds", "riser", "impact"],
    };
    #endregion

    #region Private methods
    [GeneratedRegex(@" \(\d+\)$", RegexOptions.CultureInvariant)]
    private static partial Regex CopyNumber();

    ///<summary>
    ///The instrument family of a sample (hi-hat for a closed hi-hat, kick for a kick), which is what a column is for.
    ///</summary>
    private static string FamilyOf(Sample sample) =>
        Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out InstrumentConcept? concept) && concept.Depth >= 2 ? concept.Family.Key : sample.Classification.Instrument.Value;

    private static IEnumerable<(LaunchpadPad Pad, Sample Sample)> SamplesOf(IEnumerable<LaunchpadPad> pads, SampleIndex index) =>
        pads.Select(pad => (pad, SampleOf(pad, index))).Where(pair => pair.Item2 is not null).Select(pair => (pair.pad, pair.Item2!));

    private static RecommendationContext ContextFor(LaunchpadProject project, int bank, int padIndex, SampleIndex index)
    {
        int column = padIndex % LaunchpadProject.ColumnCount;
        List<(LaunchpadPad Pad, Sample Sample)> all = [.. SamplesOf(project.Pads, index)];
        Sample? replacing = all.FirstOrDefault(pair => pair.Pad.Bank == bank && pair.Pad.Index == padIndex).Sample;
        List<Sample> neighbours = [.. all.Where(pair => pair.Pad.Bank == bank && pair.Pad.Index % LaunchpadProject.ColumnCount == column && pair.Pad.Index != padIndex).Select(pair => pair.Sample)];

        IReadOnlyList<string> instruments = replacing is not null
            ? [FamilyOf(replacing)]
            : neighbours.Count > 0 ? [.. neighbours.Select(FamilyOf).Distinct(StringComparer.Ordinal)] : _laneInstruments[column];

        bool? wantsLoop = replacing is not null
            ? KeyCompatibility.IsPassage(replacing)
            : neighbours.Count == 0 ? null : neighbours.All(KeyCompatibility.IsPassage) ? true : neighbours.Any(KeyCompatibility.IsPassage) ? null : false;

        return new RecommendationContext
        {
            InUse = [.. all.Select(pair => pair.Sample)],
            Neighbours = neighbours,
            SlotInstruments = instruments,
            Key = KeyOf(project, index),
            TempoBpm = project.Tempo,
            Replacing = replacing,
            WantsLoop = wantsLoop,
        };
    }

    private static string NameOf(Sample sample) => Path.GetFileNameWithoutExtension(sample.Id);
    #endregion

    #region Public methods
    ///<summary>
    ///The Sound Bank sample a pad plays, found from its file's name (a copy's " (2)" is ignored) or else its label; null for a
    ///sound that is not from the Sound Bank.
    ///</summary>
    public static Sample? SampleOf(LaunchpadPad pad, SampleIndex index)
    {
        if (pad.ClipPath is { Length: > 0 } path)
        {
            string stem = CopyNumber().Replace(Path.GetFileNameWithoutExtension(path), string.Empty);
            if (index.Find(stem + ".wav") is { } byFile)
            {
                return byFile;
            }
        }

        return string.IsNullOrWhiteSpace(pad.Label) ? null : index.Find(pad.Label + ".wav");
    }

    ///<summary>
    ///The project's key: its own if it has one, otherwise the key its pitched sounds point to, or null.
    ///</summary>
    public static Key? KeyOf(LaunchpadProject project, SampleIndex index) =>
        LaunchpadScale.KeyOf(project) ?? SampleRecommender.InferKey(SamplesOf(project.Pads, index).Select(pair => pair.Sample));

    ///<summary>
    ///Up to <paramref name="count"/> sounds for pad <paramref name="padIndex"/> of bank <paramref name="bank"/>, best first. For a pad
    ///that has a sound, they are alternatives like it; for an empty pad, sounds for what its column holds.
    ///</summary>
    public static IReadOnlyList<LaunchpadSuggestion> Suggest(LaunchpadProject project, int bank, int padIndex, int count, SampleIndex index)
    {
        RecommendationContext context = ContextFor(project, bank, padIndex, index);
        return [.. new SampleRecommender(index).Recommend(context, count).Select(r => new LaunchpadSuggestion(NameOf(r.Sample), r.Reasons))];
    }

    ///<summary>
    ///Sounds for the empty pads of a column, bottom row first, chosen together so they differ from each other.
    ///</summary>
    public static IReadOnlyList<LaunchpadPadAssignment> FillColumn(LaunchpadProject project, int bank, int column, SampleIndex index)
    {
        HashSet<int> taken = [.. project.Pads.Where(pad => pad.Bank == bank && pad.HasClip).Select(pad => pad.Index)];
        List<int> empty = [.. Enumerable.Range(0, Rows).Reverse().Select(row => (row * LaunchpadProject.ColumnCount) + column).Where(pad => !taken.Contains(pad))];
        if (empty.Count == 0)
        {
            return [];
        }

        IReadOnlyList<LaunchpadSuggestion> suggestions = Suggest(project, bank, empty[0], empty.Count, index);
        return [.. empty.Zip(suggestions, (pad, suggestion) => new LaunchpadPadAssignment(pad, suggestion.SampleName))];
    }

    ///<summary>
    ///For each drum on bank <paramref name="bank"/>, the nearest sound of the same instrument from kit <paramref name="kitKey"/>.
    ///Pads that are not drums, or whose instrument the kit lacks, are left alone.
    ///</summary>
    public static IReadOnlyList<LaunchpadPadAssignment> SwapKit(LaunchpadProject project, int bank, string kitKey, SampleIndex index)
    {
        SampleRecommender recommender = new(index);
        List<(LaunchpadPad Pad, Sample Sample)> all = [.. SamplesOf(project.Pads, index)];
        HashSet<string> chosen = [];
        List<LaunchpadPadAssignment> swaps = [];
        foreach ((LaunchpadPad pad, Sample sample) in all.Where(pair => pair.Pad.Bank == bank).OrderBy(pair => pair.Pad.Index))
        {
            bool isDrum = Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out InstrumentConcept? concept) && concept.IsA("percussion") && !concept.IsA("tuned-percussion");
            if (!isDrum || KeyCompatibility.IsPassage(sample))
            {
                continue;
            }

            RecommendationContext context = new()
            {
                InUse = [.. all.Select(pair => pair.Sample).Where(other => other.Id != sample.Id)],
                SlotInstruments = [sample.Classification.Instrument.Value],
                Replacing = sample,
                Kit = kitKey,
                WantsLoop = false,
            };
            Recommendation? best = recommender.Recommend(context, 8).FirstOrDefault(r => !chosen.Contains(r.Sample.Id));
            if (best is not null)
            {
                _ = chosen.Add(best.Sample.Id);
                swaps.Add(new LaunchpadPadAssignment(pad.Index, NameOf(best.Sample)));
            }
        }

        return swaps;
    }
    #endregion
}
