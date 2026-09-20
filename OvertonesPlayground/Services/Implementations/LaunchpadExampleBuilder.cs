using System.Text.RegularExpressions;
using OvertonesPlayground.Models;
using OvertonesPlayground.Ontology.Facets;
using OvertonesPlayground.Ontology.Model;
using OvertonesPlayground.Ontology.Querying;

namespace OvertonesPlayground.Services.Implementations;

///<summary>
///How loud a kind of sound should sit in a setup. Each has a target loudness (LUFS), and a pad's volume is what brings that
///sound to it.
///</summary>
internal enum ExampleLevel
{
    Kick,
    Snare,
    Clap,
    Rim,
    Hat,
    HandPercussion,
    Tom,
    Bass,
    Beat,
    Music,
    Tops,
    Texture,
    Fx,
}

///<summary>
///A sample on a pad: where it is, what it is for, and the gain (0 to 1) that brings it to its level.
///</summary>
internal sealed record ExamplePlacement(int Bank, int Lane, int Row, Sample Sample, ExampleLevel Level, double Gain);

///<summary>
///Assembles a <see cref="LaunchpadProject"/> out of samples chosen from the ontology, and knows the rules that make them belong
///together. The Launchpad's column mixer is shared by all four banks, so a column is a <em>lane</em>: it holds the same kind of
///sound in every bank (a kick and a bass loop share the low lane, and so do their volume, pan and echo).
///</summary>
///<remarks>
///The sequencer picks a track's sample from the bottom four pad rows, so the sounds a setup sequences sit there, with the most
///typical one on the bottom row. A pad can only be made quieter (its volume tops out at 1), so the level targets sit on the quiet
///side and a sound that is quieter than its target is left at full volume.
///</remarks>
internal sealed class LaunchpadExampleBuilder
{
    #region Constants
    ///<summary>Lane of kicks, bass loops and bass notes.</summary>
    internal const int Low = 0;

    ///<summary>Lane of snares and drum loops.</summary>
    internal const int Backbeat = 1;

    ///<summary>Lane of claps and rims. It has echo, so it never holds a loop.</summary>
    internal const int Accent = 2;

    ///<summary>Lane of hi-hats (closed and open together, so Radio lets a closed one cut an open one).</summary>
    internal const int Hats = 3;

    ///<summary>Lane of cymbals and percussion loops.</summary>
    internal const int Cymbals = 4;

    ///<summary>Lane of hand percussion and melodic loops.</summary>
    internal const int Percussion = 5;

    ///<summary>Lane of toms and textures.</summary>
    internal const int Toms = 6;

    ///<summary>Lane of bass notes, effects and harmony loops.</summary>
    internal const int Colour = 7;

    ///<summary>Number of pad rows in a bank.</summary>
    internal const int Rows = LaunchpadProject.PadsPerBank / LaunchpadProject.ColumnCount;

    ///<summary>The bottom row, where a lane's most typical sound goes.</summary>
    internal const int PrimaryRow = Rows - 1;

    ///<summary>The top of the bottom four rows: the only pads the sequencer can take a track's sample from.</summary>
    internal const int FirstSelectableRow = Rows / 2;

    ///<summary>Largest tempo mismatch, as a fraction, that a loop can have and still stay in time over a few bars.</summary>
    internal const double MaxTempoError = 0.005;

    ///<summary>Kicks with more than this share of their energy below 60 Hz would mask an 808 bass.</summary>
    internal const double MaxKickSubWithBass = 0.35;

    ///<summary>The worst mono compatibility (dB) a sound in the centre of the mix may have.</summary>
    internal const double MinCentreMonoCompatibilityDb = -1.5;

    private const double MinConfidence = 0.85;
    private const int SequencerTracks = LaunchpadPattern.TrackCount;

    private static readonly Dictionary<ExampleLevel, double> _targetLufs = new()
    {
        [ExampleLevel.Kick] = -15,
        [ExampleLevel.Snare] = -16,
        [ExampleLevel.Clap] = -18,
        [ExampleLevel.Rim] = -20,
        [ExampleLevel.Hat] = -23,
        [ExampleLevel.HandPercussion] = -22,
        [ExampleLevel.Tom] = -18,
        [ExampleLevel.Bass] = -14,
        [ExampleLevel.Beat] = -17,
        [ExampleLevel.Music] = -21,
        [ExampleLevel.Tops] = -24,
        [ExampleLevel.Texture] = -28,
        [ExampleLevel.Fx] = -20,
    };

    private static readonly int[] _minorScale = [0, 2, 3, 5, 7, 8, 10];
    private static readonly int[] _majorScale = [0, 2, 4, 5, 7, 9, 11];
    #endregion

    #region Fields
    private readonly SampleIndex _index;
    private readonly Func<Sample, string> _pathOf;
    private readonly List<ExamplePlacement> _placements = [];
    private readonly double[] _trackGain = [1, 1, 1, 1];
    #endregion

    #region Constructors
    ///<summary>
    ///Starts a setup.
    ///</summary>
    ///<param name="index">The sound bank.</param>
    ///<param name="pathOf">The file path a pad or track should use for a sample.</param>
    ///<param name="tempo">Tempo in BPM.</param>
    ///<param name="swingLevel">Swing, as one of the Launchpad's eight levels.</param>
    ///<param name="scaleIndex">Scale for Note and Chord modes (see <see cref="LaunchpadScale"/>).</param>
    internal LaunchpadExampleBuilder(SampleIndex index, Func<Sample, string> pathOf, int tempo, int swingLevel, int scaleIndex)
    {
        _index = index;
        _pathOf = pathOf;
        Project = new LaunchpadProject
        {
            Tempo = tempo,
            SwingLevel = swingLevel,
            ScaleIndex = scaleIndex,
            IsRadioOn = true,
            MasterVolume = 0.9,
        };
    }
    #endregion

    #region Private methods
    ///<summary>
    ///A loop's name without the tempo at the end ("Lava Beat 130 bpm" becomes "Lava Beat"): every loop of a setup is at the
    ///setup's tempo, and the pad is too small to spare the room.
    ///</summary>
    internal static string WithoutTempo(string name) => Regex.Replace(name, @"\s*\d+\s*bpm$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static (bool IsOn, double Velocity, int Probability) Decode(char step) => step switch
    {
        'X' => (true, 1.0, 100),
        'x' => (true, 0.75, 100),
        'o' => (true, 0.5, 100),
        '-' => (true, 0.25, 100),
        '?' => (true, 0.5, 50),
        '.' => (false, 0, 100),
        _ => throw new ArgumentException($"'{step}' is not a step: use X, x, o, - (loud to quiet), ? (half the time) or . (rest)."),
    };
    #endregion

    #region Selection
    ///<summary>
    ///Whether a sample is analysed and confidently classified, so it can be picked for what it is called.
    ///</summary>
    internal static bool IsUsable(Sample sample) =>
        sample.Status == AnalysisStatus.Analyzed
        && sample.Spectral is not null
        && sample.Dynamics is not null
        && sample.Stereo is not null
        && sample.Technical is not null
        && !sample.Classification.NeedsReview
        && sample.Classification.Instrument.Confidence >= MinConfidence;

    ///<summary>
    ///Whether a sample can sit in the centre of a mix: one channel, two identical ones or two nearly identical ones, and one that
    ///does not thin out when summed to mono.
    ///</summary>
    internal static bool IsSolidCentre(Sample sample) =>
        IsUsable(sample)
        && (sample.Stereo!.Image is StereoImage.Mono or StereoImage.DualMono or StereoImage.Narrow)
        && sample.Stereo.MonoCompatibilityDb >= MinCentreMonoCompatibilityDb;

    ///<summary>Spectral centroid in Hz.</summary>
    internal static double Centroid(Sample sample) => sample.Spectral?.CentroidHz ?? 0;

    ///<summary>Length in seconds.</summary>
    internal static double Duration(Sample sample) => sample.Technical?.DurationSeconds ?? 0;

    ///<summary>Share of the energy below 60 Hz.</summary>
    internal static double Sub(Sample sample) => sample.Spectral?.Bands.Sub ?? 0;

    ///<summary>
    ///Whether the key written in a sample's name sits in the key of a setup, so the two can sound together. A sound with no key
    ///written in its name (a drum, a texture) has nothing to clash. A key with no mode must be a note of the scale; a minor key
    ///must be the tonic of a minor setup (or the relative minor of a major one); a major key must be the relative major of a minor
    ///setup (or its tonic).
    ///</summary>
    internal static bool FitsKey(Sample sample, int rootPitchClass, bool isMinor)
    {
        if (sample.Classification.Attributes.KeyPitchClass is not { } pitchClass)
        {
            return true;
        }

        int degree = (((pitchClass - rootPitchClass) % 12) + 12) % 12;
        if (!(isMinor ? _minorScale : _majorScale).Contains(degree))
        {
            return false;
        }

        return sample.Classification.Attributes.KeyMode switch
        {
            KeyMode.Minor => isMinor ? degree == 0 : degree == 9,
            KeyMode.Major => isMinor ? degree == 3 : degree == 0,
            _ => true,
        };
    }

    ///<summary>
    ///Whether a loop runs at <paramref name="bpm"/> (from the tempo written in its name) closely enough to stay in time.
    ///</summary>
    internal static bool RunsAt(Sample sample, double bpm) =>
        sample.Classification.Attributes.TempoBpm is { } tempo && Math.Abs(tempo - bpm) / bpm <= MaxTempoError;

    ///<summary>
    ///The gain (0.05 to 1) that brings <paramref name="sample"/> to the loudness of <paramref name="level"/>. It is rounded down,
    ///to a thousandth, so a sound is never a hair louder than its target.
    ///</summary>
    internal static double Gain(Sample sample, ExampleLevel level)
    {
        double target = _targetLufs[level];
        double lufs = sample.Dynamics?.IntegratedLufs ?? target;
        double gain = Math.Floor(Math.Pow(10, (target - lufs) / 20.0) * 1000.0) / 1000.0;
        return Math.Clamp(gain, 0.05, 1.0);
    }

    ///<summary>The loudness (LUFS) a kind of sound is brought to.</summary>
    internal static double TargetLufs(ExampleLevel level) => _targetLufs[level];

    ///<summary>
    ///<paramref name="count"/> samples spread evenly across the range of <paramref name="by"/>, so the pads offer a real choice
    ///(dark to bright, short to long) instead of eight near-copies.
    ///</summary>
    internal static IReadOnlyList<Sample> Spread(IEnumerable<Sample> candidates, int count, Func<Sample, double> by)
    {
        List<Sample> sorted = [.. candidates.OrderBy(by).ThenBy(sample => sample.Id, StringComparer.Ordinal)];
        if (sorted.Count <= count)
        {
            return sorted;
        }

        return [.. Enumerable.Range(0, count).Select(i => sorted[(int)Math.Round(i * (sorted.Count - 1) / (double)(count - 1))]).DistinctBy(sample => sample.Id)];
    }

    ///<summary>
    ///The samples reordered so the most typical one (nearest the median of <paramref name="by"/>) comes first, the way a lane's
    ///primary pad should be.
    ///</summary>
    internal static IReadOnlyList<Sample> TypicalFirst(IReadOnlyList<Sample> samples, Func<Sample, double> by)
    {
        if (samples.Count == 0)
        {
            return samples;
        }

        double median = samples.Select(by).Order().ElementAt(samples.Count / 2);
        return [.. samples.OrderBy(sample => Math.Abs(by(sample) - median)).ThenBy(sample => sample.Id, StringComparer.Ordinal)];
    }

    ///<summary>
    ///Spreads <paramref name="pool"/> over <paramref name="count"/> pads with the most typical sound first.
    ///</summary>
    internal static IReadOnlyList<Sample> Choose(IEnumerable<Sample> pool, int count, Func<Sample, double> by) =>
        TypicalFirst(Spread(pool, count, by), by);

    ///<summary>
    ///Two closed hats, two open ones, two closed ... so both kinds are among the pads the sequencer can pick from and a closed hat
    ///sits next to the open one it cuts.
    ///</summary>
    internal static IReadOnlyList<Sample> Interleave(IReadOnlyList<Sample> closed, IReadOnlyList<Sample> open)
    {
        List<Sample> result = [];
        int c = 0;
        int o = 0;
        while (result.Count < Rows && (c < closed.Count || o < open.Count))
        {
            for (int i = 0; i < 2 && c < closed.Count && result.Count < Rows; i++)
            {
                result.Add(closed[c++]);
            }

            for (int i = 0; i < 2 && o < open.Count && result.Count < Rows; i++)
            {
                result.Add(open[o++]);
            }
        }

        return result;
    }

    ///<summary>
    ///Pairs each sample with the level it is for, the form <see cref="Fill(int, int, IEnumerable{ValueTuple{Sample, ExampleLevel}}, bool)"/> takes.
    ///</summary>
    internal static IEnumerable<(Sample Sample, ExampleLevel Level)> Tag(IEnumerable<Sample> samples, ExampleLevel level) =>
        samples.Select(sample => (Sample: sample, Level: level));

    ///<summary>
    ///The usable samples of an instrument (and, if given, a kit), in name order.
    ///</summary>
    internal IEnumerable<Sample> Pool(string instrument, string? kit = null)
    {
        SampleSpecification specification = SampleSpecs.InstrumentIs(instrument);
        if (kit is not null)
        {
            specification &= SampleSpecs.KitIs(kit);
        }

        return _index.Query(new SampleQuery { Filter = specification }).Where(IsUsable);
    }

    ///<summary>
    ///The usable samples of an instrument that can sit in the centre of the mix.
    ///</summary>
    internal IEnumerable<Sample> Solid(string instrument, string? kit = null) => Pool(instrument, kit).Where(IsSolidCentre);

    ///<summary>
    ///Bass notes whose pitch is one of <paramref name="pitchClasses"/>, judged by name or by a confident measurement.
    ///</summary>
    internal IEnumerable<Sample> BassNotes(IReadOnlyCollection<int> pitchClasses) =>
        Pool("bass-808").Where(sample => sample.EffectivePitchClass is { } pitchClass && pitchClasses.Contains(pitchClass) && (sample.Classification.Attributes.KeyPitchClass is not null || sample.Tonality is { PitchConfidence: >= 0.9 }));

    ///<summary>
    ///A specific sample of the bank, by its name without the extension.
    ///</summary>
    ///<exception cref="InvalidOperationException">The bank has no such sample.</exception>
    internal Sample Named(string name) =>
        _index.Find(name + ".wav") ?? throw new InvalidOperationException($"The sound bank has no sample called '{name}'.");

    ///<summary>
    ///A loop of the bank that is at the setup's tempo and in its key. Asking for one that is not is a mistake in the recipe, so
    ///it fails rather than putting a clashing loop into the setup.
    ///</summary>
    ///<exception cref="InvalidOperationException">The loop is missing, at another tempo, or in another key.</exception>
    internal Sample Loop(string name, int rootPitchClass, bool isMinor)
    {
        Sample sample = Named(name);
        if (!RunsAt(sample, Project.Tempo))
        {
            throw new InvalidOperationException($"'{name}' is not at {Project.Tempo} BPM, so it would drift out of time.");
        }

        return FitsKey(sample, rootPitchClass, isMinor)
            ? sample
            : throw new InvalidOperationException($"'{name}' is not in the key of the setup, so it would clash.");
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Puts <paramref name="samples"/> on a lane of a bank, the first on the bottom row and the rest above it.
    ///</summary>
    internal void Fill(int bank, int lane, ExampleLevel level, IEnumerable<Sample> samples, bool loop = false) =>
        Fill(bank, lane, samples.Select(sample => (sample, level)), loop);

    ///<summary>
    ///Puts samples, each with its own level, on a lane of a bank, the first on the bottom row and the rest above it.
    ///</summary>
    internal void Fill(int bank, int lane, IEnumerable<(Sample Sample, ExampleLevel Level)> items, bool loop = false)
    {
        int row = PrimaryRow;
        foreach ((Sample sample, ExampleLevel level) in items)
        {
            if (row < 0)
            {
                throw new InvalidOperationException($"Lane {lane} of bank {bank} is full.");
            }

            double gain = Gain(sample, level);
            _placements.Add(new ExamplePlacement(bank, lane, row, sample, level, gain));
            Project.Pads.Add(new LaunchpadPad
            {
                Bank = bank,
                Index = (row * LaunchpadProject.ColumnCount) + lane,
                ClipPath = _pathOf(sample),
                Label = loop ? WithoutTempo(sample.Name) : sample.Name,
                ColorHex = LaunchpadColumn.Colors[lane],
                IsLooping = loop,
                Volume = gain,
            });
            row--;
        }
    }

    ///<summary>
    ///Sets a lane's share of the column mixer: its pan, its echo (each step of 0.25 adds one quieter repeat an eighth note apart)
    ///and its volume.
    ///</summary>
    internal void Mix(int lane, double pan = 0, double send = 0, double volume = 1)
    {
        LaunchpadColumn column = Project.Columns[lane];
        column.Pan = pan;
        column.Send = send;
        column.Volume = volume;
    }

    ///<summary>
    ///Writes steps into a pattern, one string per track: X, x, o and - are steps from loud to quiet, ? is a step that plays half
    ///the time, . is a rest. The pattern is as long as the longest string.
    ///</summary>
    internal void Pattern(int pattern, params (int Track, string Steps)[] lines)
    {
        LaunchpadPattern target = Project.Sequence.Patterns[pattern];
        target.Length = lines.Max(line => line.Steps.Length);
        foreach ((int track, string steps) in lines)
        {
            for (int i = 0; i < steps.Length; i++)
            {
                (bool isOn, double velocity, int probability) = Decode(steps[i]);
                if (isOn)
                {
                    LaunchpadStep step = target.Tracks[track][i];
                    step.IsOn = true;
                    step.Velocity = Math.Round(velocity * _trackGain[track], 3);
                    step.Probability = probability;
                }
            }
        }
    }

    ///<summary>
    ///Gives sequencer track <paramref name="track"/> the sample on a pad, which must be one of the bottom four rows (the ones the
    ///Launchpad lets a track choose from). The track plays through that pad's column, and its steps are as loud as the pad would be.
    ///</summary>
    internal void Track(int track, int bank, int lane, int row = PrimaryRow)
    {
        if (row < FirstSelectableRow)
        {
            throw new ArgumentOutOfRangeException(nameof(row), "The sequencer can only take a track's sample from the bottom four rows.");
        }

        ExamplePlacement placement = _placements.Single(p => p.Bank == bank && p.Lane == lane && p.Row == row);
        Project.Sequence.Tracks[track] = new LaunchpadTrack
        {
            ClipPath = _pathOf(placement.Sample),
            Label = placement.Sample.Name,
            ColorHex = LaunchpadColumn.Colors[lane],
            Column = lane,
        };
        _trackGain[track] = placement.Gain;
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Every sample placed on a pad so far.
    ///</summary>
    internal IReadOnlyList<ExamplePlacement> Placements => _placements;

    ///<summary>
    ///The setup being assembled.
    ///</summary>
    internal LaunchpadProject Project { get; }

    ///<summary>
    ///The keys of the kits in the bank, in name order.
    ///</summary>
    internal IEnumerable<string> KitKeys => _index.GetKits().Select(kit => kit.Concept.Key).Order(StringComparer.Ordinal);

    ///<summary>
    ///The number of sequencer tracks.
    ///</summary>
    internal static int TrackCount => SequencerTracks;
    #endregion
}
