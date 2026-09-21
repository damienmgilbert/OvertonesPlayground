using System.Globalization;
using System.Runtime.CompilerServices;
using OvertonesPlayground.Models;
using OvertonesPlayground.Ontology.Concepts;
using OvertonesPlayground.Ontology.Model;
using OvertonesPlayground.Ontology.Querying;
using OvertonesPlayground.Ontology.Styles;
using OvertonesPlayground.Ontology.Theory;
using static OvertonesPlayground.Services.Implementations.ExampleLevel;
using static OvertonesPlayground.Services.Implementations.LaunchpadExampleBuilder;

namespace OvertonesPlayground.Services.Implementations;

///<summary>
///Makes Launchpad setups from a style profile and a seed, by music theory and the rules the hand-written examples follow. Every
///choice is random but ruled: the tempo is in the style's range (and, where the bank has loops at one of its tempos, often that
///one), the key is one the bank has pitched sounds for, the drums come from a kit that suits the style with anything it lacks taken
///from similar kits, every pitched sound is in the key, every loop is at the tempo, and the patterns come from the style's library
///with variations a drummer would play (ghost notes, nudges, a breakdown and a fill). A setup that fails
///<see cref="LaunchpadQuality"/> is made again with the next seed.
///</summary>
///<remarks>
///The banks: A is the drum kit (one instrument role per column, the way the examples lay it out); B holds loops at the tempo; C holds
///harmony in the key (chords, notes, bass); D holds effects and textures. The sequencer gets kick, backbeat, hats and the style's
///fourth part, in four patterns: the groove, a variation, a breakdown and a fill.
///</remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Seeded musical choices, not security: the same seed must give the same project.")]
internal static class LaunchpadGenerator
{
    #region Constants
    ///<summary>How many seeds are tried before giving up on a request.</summary>
    internal const int MaxAttempts = 16;

    private const int KitBank = 0;
    private const int LoopBank = 1;
    private const int HarmonyBank = 2;
    private const int EffectsBank = 3;

    ///<summary>Seeds of successive attempts are this far apart, so they do not overlap the seeds of nearby requests.</summary>
    private const int SeedStride = 104_729;

    private static readonly string[] _bassInstruments = ["bass-808", "bass-sub", "bass-synth", "bass-electric"];
    private static readonly string[] _chordInstruments = ["stab", "synth-chord", "synth-pad", "synth", "e-piano", "organ", "piano", "guitar"];
    private static readonly string[] _noteInstruments = ["mallet", "synth-note", "bell", "e-piano", "piano", "organ", "orchestral-strings", "guitar"];
    #endregion

    #region Fields
    private static readonly ConditionalWeakTable<SampleIndex, Pools> _pools = [];
    #endregion

    #region Private types
    ///<summary>
    ///The bank's usable samples, grouped the ways the generator asks for them. Built once per index.
    ///</summary>
    private sealed class Pools
    {
        public Pools(SampleIndex index)
        {
            Index = index;
            Usable = [.. index.All.Where(IsUsable)];
            Passages = [.. index.All.Where(sample => sample.Status == AnalysisStatus.Analyzed && KeyCompatibility.IsPassage(sample) && sample.Classification.Attributes.TempoBpm is not null)];
            Pitched = [.. Usable.Where(sample => KeyCompatibility.PitchOf(sample) is not null)];
        }

        public SampleIndex Index { get; }

        public IReadOnlyList<Sample> Usable { get; }

        public IReadOnlyList<Sample> Passages { get; }

        public IReadOnlyList<Sample> Pitched { get; }

        public System.Collections.Concurrent.ConcurrentDictionary<(string Scale, int Root, int Tempo), int> Material { get; } = new();

        public IEnumerable<Sample> Of(params string[] instruments) => Usable.Where(sample => IsA(sample, instruments));
    }

    ///<summary>
    ///The state of one run of the generator.
    ///</summary>
    private sealed class Draft(Pools pools, StyleProfile profile, Random random, Key key, int tempo, string kit)
    {
        public Pools Pools { get; } = pools;

        public StyleProfile Profile { get; } = profile;

        public Random Random { get; } = random;

        public Key Key { get; } = key;

        public int Tempo { get; } = tempo;

        public string Kit { get; } = kit;

        public List<RecipeFill> Fills { get; } = [];

        public HashSet<string> Used { get; } = new(StringComparer.Ordinal);

        public int CountAt(int bank, int lane) => Fills.Where(fill => fill.Bank == bank && fill.Lane == lane).Sum(fill => fill.Sounds.Count);

        public void Fill(int bank, int lane, ExampleLevel level, IEnumerable<Sample> samples, bool loop = false) =>
            Fill(bank, lane, samples.Select(sample => (sample, level)), loop);

        public void Fill(int bank, int lane, IEnumerable<(Sample Sample, ExampleLevel Level)> items, bool loop = false)
        {
            int room = Rows - CountAt(bank, lane);
            List<(string, ExampleLevel)> sounds = [.. items.Where(item => !IsUsedIn(bank, item.Sample)).DistinctBy(item => item.Sample.Id).Take(room).Select(item => (item.Sample.Id, item.Level))];
            if (sounds.Count > 0)
            {
                Fills.Add(new RecipeFill(bank, lane, sounds, loop));
                foreach ((string id, _) in sounds)
                {
                    _ = Used.Add($"{bank}:{id}");
                }
            }
        }

        public bool IsUsedIn(int bank, Sample sample) => Used.Contains($"{bank}:{sample.Id}");
    }
    #endregion

    #region Private methods
    private static bool InKit(Sample sample, string kit) =>
        sample.Classification.Kit is { } label && Taxonomies.Kits.TryGet(label.Value, out KitConcept? concept) && concept.IsA(kit);

    ///<summary>
    ///<paramref name="count"/> samples spread from dark to bright (see <see cref="Spread"/>), for any count including one.
    ///</summary>
    private static IReadOnlyList<Sample> SpreadAny(IReadOnlyList<Sample> samples, int count) => count switch
    {
        <= 0 => [],
        1 => [.. TypicalFirst(samples, Centroid).Take(1)],
        _ => Spread(samples, count, Centroid),
    };

    private static bool IsA(Sample sample, IReadOnlyList<string> instruments) =>
        Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out InstrumentConcept? concept) && instruments.Any(concept.IsA);

    ///<summary>
    ///An item of <paramref name="items"/>, the earlier ones likelier: the first of a style's lists is its most typical choice.
    ///</summary>
    private static T PickTypical<T>(Random random, IReadOnlyList<T> items)
    {
        // Weights n, n-1 ... 1.
        int n = items.Count;
        int total = n * (n + 1) / 2;
        int roll = random.Next(total);
        for (int i = 0; i < n; i++)
        {
            roll -= n - i;
            if (roll < 0)
            {
                return items[i];
            }
        }

        return items[^1];
    }

    private static T Pick<T>(Random random, IReadOnlyList<T> items) => items[random.Next(items.Count)];

    ///<summary>
    ///The samples in a random order that is the same for the same seed.
    ///</summary>
    private static List<Sample> Shuffle(Random random, IEnumerable<Sample> samples)
    {
        List<Sample> list = [.. samples.OrderBy(sample => sample.Id, StringComparer.Ordinal)];
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    ///<summary>
    ///Up to <paramref name="count"/> samples of an instrument for the kit bank: the draft's kit first, then the style's other kits,
    ///then any sample of the same origin, then anything. Within each group the choice is random but spread from dark to bright.
    ///</summary>
    private static IReadOnlyList<Sample> Drums(Draft draft, IEnumerable<Sample> pool, int count)
    {
        List<Sample> candidates = [.. pool.Where(sample => !draft.IsUsedIn(KitBank, sample))];
        SoundOrigin? origin = Taxonomies.Kits.TryGet(draft.Kit, out KitConcept? kitConcept) ? kitConcept.Origin : null;

        IEnumerable<IEnumerable<Sample>> tiers =
        [
            candidates.Where(sample => InKit(sample, draft.Kit)),
            candidates.Where(sample => draft.Profile.Kits.Any(kit => kit != draft.Kit && InKit(sample, kit))),
            candidates.Where(sample => origin is not null && sample.Classification.Origin.Value == origin),
            candidates,
        ];

        List<Sample> chosen = [];
        foreach (IEnumerable<Sample> tier in tiers)
        {
            if (chosen.Count >= count)
            {
                break;
            }

            List<Sample> fresh = [.. tier.Where(sample => chosen.All(other => other.Id != sample.Id))];
            List<Sample> picks = [.. Shuffle(draft.Random, fresh).Take(Math.Max(count * 2, 4))];
            chosen.AddRange(SpreadAny(picks, count - chosen.Count));
        }

        return TypicalFirst(chosen, Centroid);
    }

    private static int ChooseTempo(StyleProfile profile, Pools pools, Random random, int? requested)
    {
        if (requested is { } tempo)
        {
            return Math.Clamp(tempo, 40, 240);
        }

        // Tempos in the style's range that the bank has loops at, so bank B has something to hold.
        List<int> loopTempos = [.. pools.Passages
            .Select(sample => sample.Classification.Attributes.TempoBpm!.Value)
            .Where(bpm => profile.Tempo.Contains(bpm) && Math.Abs(bpm - Math.Round(bpm)) < 0.01)
            .Select(bpm => (int)Math.Round(bpm))
            .Distinct()
            .Order()];
        bool useLoopTempo = loopTempos.Count > 0 && random.NextDouble() < 0.65;
        return useLoopTempo ? Pick(random, loopTempos) : random.Next(profile.Tempo.Min, profile.Tempo.Max + 1);
    }

    ///<summary>
    ///How much pitched material the bank has in <paramref name="key"/> at <paramref name="tempo"/>: bass notes, chords, notes and
    ///loops that fit it.
    ///</summary>
    private static int Material(Pools pools, Key key, int tempo) =>
        pools.Material.GetOrAdd((key.Scale.Key, key.Tonic.Value, tempo), _ => CountMaterial(pools, key, tempo));

    private static int CountMaterial(Pools pools, Key key, int tempo)
    {
        int oneShots = pools.Pitched.Count(sample => KeyCompatibility.PitchOf(sample) is not null && KeyCompatibility.Judge(sample, key) == KeyFit.Fits);
        int loops = pools.Passages.Count(sample => RunsAt(sample, tempo) && KeyCompatibility.Judge(sample, key) == KeyFit.Fits);
        int roots = pools.Of(_bassInstruments).Count(sample => KeyCompatibility.PitchOf(sample) == key.Tonic);
        return oneShots + (3 * loops) + (2 * roots);
    }

    private static Key ChooseKey(StyleProfile profile, Pools pools, Random random, int tempo, string? requested)
    {
        if (requested is not null)
        {
            return Key.Parse(requested);
        }

        Scale scale = PickTypical(random, profile.ScaleList);

        // Weigh each root by how much the bank can play in it, so the harmony bank is well stocked.
        List<(int Root, int Weight)> roots = [.. Enumerable.Range(0, 12).Select(root => (root, Material(pools, new Key(new PitchClass(root), scale), tempo)))];
        int total = roots.Sum(pair => pair.Weight * pair.Weight);
        if (total == 0)
        {
            return new Key(new PitchClass(random.Next(12)), scale);
        }

        int roll = random.Next(total);
        foreach ((int root, int weight) in roots)
        {
            roll -= weight * weight;
            if (roll < 0)
            {
                return new Key(new PitchClass(root), scale);
            }
        }

        return new Key(PitchClass.C, scale);
    }

    private static string ChooseKit(StyleProfile profile, Pools pools, Random random, string? requested)
    {
        if (requested is not null)
        {
            return requested;
        }

        List<string> present = [.. profile.Kits.Where(kit => pools.Usable.Count(sample => InKit(sample, kit)) >= 8)];
        return present.Count > 0 ? PickTypical(random, present) : profile.Kits.FirstOrDefault() ?? "vinyl";
    }

    private static void FillKitBank(Draft draft)
    {
        StyleProfile profile = draft.Profile;
        bool hasBass = profile.Uses808 || profile.Extra == StyleExtraPart.Bass;

        IEnumerable<Sample> kicks = draft.Pools.Of("kick").Where(k => IsSolidCentre(k) && Duration(k) <= 0.8 && (!hasBass || Sub(k) <= MaxKickSubWithBass));
        draft.Fill(KitBank, Low, Kick, Drums(draft, kicks, 8));

        IEnumerable<Sample> snares = draft.Pools.Of("snare").Where(s => IsSolidCentre(s) && Duration(s) <= 0.8);
        IEnumerable<Sample> claps = draft.Pools.Of("clap").Where(c => IsUsable(c) && Duration(c) <= 1.0);
        IEnumerable<Sample> rims = draft.Pools.Of("rim", "snap").Where(r => Duration(r) <= 0.8);
        switch (profile.BackbeatSound)
        {
            case "clap":
                draft.Fill(KitBank, Backbeat, [.. Tag(Drums(draft, claps, 3), Clap), .. Tag(Drums(draft, snares, 5), Snare)]);
                draft.Fill(KitBank, Accent, [.. Tag(Drums(draft, rims, 5), Rim), .. Tag(Drums(draft, claps, 3), Clap)]);
                break;
            case "rim":
                draft.Fill(KitBank, Backbeat, [.. Tag(Drums(draft, rims, 4), Rim), .. Tag(Drums(draft, snares, 4), Snare)]);
                draft.Fill(KitBank, Accent, [.. Tag(Drums(draft, claps, 4), Clap), .. Tag(Drums(draft, rims, 4), Rim)]);
                break;
            default:
                draft.Fill(KitBank, Backbeat, [.. Tag(Drums(draft, snares, 6), Snare), .. Tag(Drums(draft, claps, 2), Clap)]);
                draft.Fill(KitBank, Accent, [.. Tag(Drums(draft, claps, 4), Clap), .. Tag(Drums(draft, rims, 4), Rim)]);
                break;
        }

        draft.Fill(KitBank, Hats, Hat, Interleave(Drums(draft, draft.Pools.Of("hihat-closed").Where(h => Duration(h) <= 0.6), 4), Drums(draft, draft.Pools.Of("hihat-open", "hihat-pedal"), 4)));
        draft.Fill(KitBank, Cymbals, Hat, Drums(draft, draft.Pools.Of("crash", "ride", "cymbal").Where(c => Duration(c) <= 5), 6));

        // Two of each of the style's percussion instruments, the most typical first, until the column is full.
        List<Sample> percussion = [];
        foreach (string instrument in profile.Percussion)
        {
            percussion.AddRange(Drums(draft, draft.Pools.Of(instrument), 2).Where(sample => percussion.All(other => other.Id != sample.Id)));
        }

        draft.Fill(KitBank, Percussion, HandPercussion, percussion.Take(Rows));
        draft.Fill(KitBank, Toms, Tom, Drums(draft, draft.Pools.Of("tom"), 8).OrderBy(Centroid));

        // A style with a bass part gets bass notes in the key on the colour lane (the root first, then the fifth, then the rest of
        // the scale), so the kicks above were chosen to leave it the sub-bass; the others get effects there, and their bass notes
        // go to the harmony bank.
        List<Sample> bass = hasBass ? BassNotes(draft, profile.Uses808 ? ["bass-808"] : _bassInstruments) : [];
        if (hasBass && bass.Count < 3 && profile.Uses808)
        {
            bass = BassNotes(draft, _bassInstruments);
        }

        if (bass.Count > 0)
        {
            draft.Fill(KitBank, Colour, Bass, bass);
        }
        else
        {
            draft.Fill(KitBank, Colour, Fx, Shuffle(draft.Random, Effects(draft, "impact", "riser")).Take(4));
        }
    }

    ///<summary>
    ///Bass notes whose pitch is in the key, ordered root, fifth, then the other degrees, at most two of each note.
    ///</summary>
    private static List<Sample> BassNotes(Draft draft, string[] instruments)
    {
        PitchClass fifth = draft.Key.Tonic.Transpose(Interval.PerfectFifth);
        return [.. Shuffle(draft.Random, draft.Pools.Of(instruments))
            .Where(sample => sample.Classification.ContentType.Value is not (ContentType.Loop or ContentType.Break) && KeyCompatibility.PitchOf(sample) is { } pitch && draft.Key.Contains(pitch) && KeyCompatibility.CanPlayIn(sample, draft.Key))
            .GroupBy(sample => KeyCompatibility.PitchOf(sample)!.Value)
            .OrderBy(group => group.Key == draft.Key.Tonic ? 0 : group.Key == fifth ? 1 : 2 + draft.Key.Tonic.SemitonesUpTo(group.Key))
            .SelectMany(group => group.Take(group.Key == draft.Key.Tonic ? 3 : 2))
            .Take(Rows)];
    }

    ///<summary>
    ///Loops at the tempo and in the key, each on the lane that suits what it plays.
    ///</summary>
    private static void FillLoopBank(Draft draft)
    {
        List<Sample> loops = [.. draft.Pools.Passages.Where(sample => RunsAt(sample, draft.Tempo) && KeyCompatibility.CanPlayIn(sample, draft.Key)).OrderBy(sample => sample.Id, StringComparer.Ordinal)];
        foreach (IGrouping<int, Sample> lane in loops.GroupBy(LoopLane).OrderBy(group => group.Key))
        {
            ExampleLevel level = lane.Key switch
            {
                Low => Bass,
                Backbeat => Beat,
                Hats or Cymbals => Tops,
                Percussion => HandPercussion,
                Toms => Texture,
                _ => Music,
            };
            draft.Fill(LoopBank, lane.Key, level, Shuffle(draft.Random, lane), loop: true);
        }
    }

    ///<summary>
    ///The lane a loop belongs on: drums on the backbeat, hats and cymbals on theirs, percussion on its lane, textures with the toms,
    ///and anything with notes on the colour lane. The accent lane has echo, so it never holds a loop.
    ///</summary>
    private static int LoopLane(Sample loop)
    {
        string instrument = loop.Classification.Instrument.Value;
        bool hasKey = loop.Classification.Attributes.KeyPitchClass is not null;
        return instrument switch
        {
            _ when IsA(loop, ["kick", "bass"]) => Low,
            _ when IsA(loop, ["hihat"]) => Hats,
            _ when IsA(loop, ["cymbal", "clap"]) => Cymbals,
            _ when IsA(loop, ["hand-percussion", "electronic-percussion"]) || instrument == "percussion" => Percussion,
            _ when IsA(loop, ["sfx"]) && !hasKey => Toms,
            _ when IsA(loop, ["drum-loop"]) || (IsA(loop, ["mixed-loop"]) && !hasKey) => Backbeat,
            _ => Colour,
        };
    }

    ///<summary>
    ///Harmony in the key: chords (the progression's chords first) on the colour lane, single notes on the percussion lane, more
    ///bass notes on the low lane and pitched textures with the toms.
    ///</summary>
    private static void FillHarmonyBank(Draft draft)
    {
        Key key = draft.Key;
        IReadOnlyList<Progression> progressions = draft.Profile.ProgressionList;
        Progression? progression = progressions.Count > 0 ? PickTypical(draft.Random, progressions) : null;
        List<PitchClass> order = progression is null ? [key.Tonic] : [.. progression.In(key).Select(chord => chord.Root).Distinct()];

        List<Sample> chords = [.. Shuffle(draft.Random, draft.Pools.Of(_chordInstruments))
            .Where(sample => !KeyCompatibility.IsPassage(sample) && Chord.TryFindInName(sample.Name, out Chord chord) && chord.Triad.IsIn(key))
            .OrderBy(sample =>
            {
                _ = Chord.TryFindInName(sample.Name, out Chord chord);
                int position = order.IndexOf(chord.Root);
                return position < 0 ? order.Count + key.Tonic.SemitonesUpTo(chord.Root) : position;
            })];
        draft.Fill(HarmonyBank, Colour, Music, chords);

        List<Sample> notes = [.. Shuffle(draft.Random, draft.Pools.Of(_noteInstruments))
            .Where(sample => !KeyCompatibility.IsPassage(sample) && !Chord.TryFindInName(sample.Name, out _) && KeyCompatibility.Judge(sample, key) == KeyFit.Fits)
            .OrderBy(sample => key.Tonic.SemitonesUpTo(KeyCompatibility.PitchOf(sample)!.Value))];
        draft.Fill(HarmonyBank, Percussion, Music, notes);

        draft.Fill(HarmonyBank, Low, Bass, BassNotes(draft, _bassInstruments).Where(sample => !draft.IsUsedIn(KitBank, sample)));

        draft.Fill(HarmonyBank, Toms, Texture, Shuffle(draft.Random, Effects(draft, "atmosphere", "noise", "synth-pad")).Take(4));
    }

    ///<summary>
    ///Effects: risers and impacts on the colour lane, vocal shots on the accent lane (whose echo suits them), textures with the toms
    ///and foley with the percussion.
    ///</summary>
    private static void FillEffectsBank(Draft draft)
    {
        draft.Fill(EffectsBank, Colour, Fx, Shuffle(draft.Random, Effects(draft, "riser", "impact")).Take(6));
        draft.Fill(EffectsBank, Accent, Fx, Shuffle(draft.Random, Effects(draft, "vocal")).Take(6));
        draft.Fill(EffectsBank, Toms, Texture, Shuffle(draft.Random, Effects(draft, "atmosphere", "noise")).Take(4));
        draft.Fill(EffectsBank, Percussion, HandPercussion, Shuffle(draft.Random, Effects(draft, "foley", "glitch", "zap").Where(sample => Duration(sample) <= 1.5)).Take(6));
    }

    ///<summary>
    ///One-shots of the given instruments that do not clash with the key (loops are left to the loop bank, which checks their tempo).
    ///</summary>
    private static IEnumerable<Sample> Effects(Draft draft, params string[] instruments) =>
        draft.Pools.Of(instruments).Where(sample => !KeyCompatibility.IsPassage(sample) && KeyCompatibility.CanPlayIn(sample, draft.Key));

    ///<summary>
    ///Which pad the fourth track plays, for the style's fourth part, falling back to whatever lane has sounds.
    ///</summary>
    private static RecipeTrack ExtraTrack(Draft draft)
    {
        (int Bank, int Lane)[] wanted = draft.Profile.Extra switch
        {
            StyleExtraPart.Bass => [(KitBank, Colour), (KitBank, Percussion)],
            StyleExtraPart.Chord => [(HarmonyBank, Colour), (KitBank, Percussion)],
            StyleExtraPart.Cymbal => [(KitBank, Cymbals), (KitBank, Percussion)],
            StyleExtraPart.Tom => [(KitBank, Toms), (KitBank, Percussion)],
            _ => [(KitBank, Percussion), (KitBank, Cymbals)],
        };
        (int Bank, int Lane)[] fallbacks = [(KitBank, Toms), (KitBank, Accent)];
        (int bank, int lane) = wanted.Concat(fallbacks).First(slot => draft.CountAt(slot.Bank, slot.Lane) > 0);
        return new RecipeTrack(3, bank, lane);
    }

    ///<summary>
    ///The four patterns: the groove, a variation (ghost notes, a nudged kick, another hat pattern), a breakdown (thinned out) and a
    ///fill (the groove with a run on the backbeat at the end).
    ///</summary>
    private static List<RecipePattern> Patterns(Draft draft, double energy)
    {
        StyleProfile profile = draft.Profile;
        Meter meter = Meter.Common;
        Random random = draft.Random;

        StepPattern kick = StepPattern.Parse(PickTypical(random, profile.Kick));
        StepPattern backbeat = StepPattern.Parse(PickTypical(random, profile.Backbeat));
        StepPattern hats = StepPattern.Parse(PickTypical(random, profile.Hats));
        StepPattern extra = profile.ExtraPatterns.Count > 0 ? StepPattern.Parse(PickTypical(random, profile.ExtraPatterns)) : kick;

        // Energy thins or thickens the hats, inside the style's range.
        if (energy < 0.35 && hats.Density > profile.MinHatDensity + 0.1)
        {
            StepPattern thinner = hats.Thin(random, meter, 0.6);
            hats = thinner.Density >= profile.MinHatDensity ? thinner : hats;
        }
        else if (energy > 0.65)
        {
            StepPattern busier = hats.AddGhosts(random, meter, 3);
            hats = busier.Density <= profile.MaxHatDensity ? busier : hats;
        }

        StepPattern otherHats = profile.Hats.Count > 1 ? StepPattern.Parse(Pick(random, profile.Hats)) : hats.AddGhosts(random, meter, 2);
        StepPattern otherExtra = profile.ExtraPatterns.Count > 1 ? StepPattern.Parse(Pick(random, profile.ExtraPatterns)) : extra.Nudge(random, meter);

        List<RecipePattern> patterns =
        [
            Pattern(0, kick, backbeat, hats.Loosen(random, meter, 0.15), extra),
            Pattern(1, kick.Nudge(random, meter), backbeat.AddGhosts(random, meter, 2), otherHats, otherExtra),
            Pattern(2, kick.Thin(random, meter, 0.3), backbeat.Thin(random, meter, 0.5), hats.Thin(random, meter, 0.5), extra.Thin(random, meter, 0.4)),
            Pattern(3, kick, backbeat.Fill(meter), hats, extra),
        ];
        return patterns;

        static RecipePattern Pattern(int index, StepPattern kick, StepPattern backbeat, StepPattern hats, StepPattern extra) =>
            new(index, [(0, kick.ToString()), (1, backbeat.ToString()), (2, hats.ToString()), (3, extra.ToString())]);
    }

    private static string Describe(Draft draft, LaunchpadRecipe recipe)
    {
        string kitName = Taxonomies.Kits.TryGet(draft.Kit, out KitConcept? kit) ? kit.DisplayName : draft.Kit;
        int loops = recipe.Fills.Where(fill => fill.Bank == LoopBank).Sum(fill => fill.Sounds.Count);
        int harmony = recipe.Fills.Where(fill => fill.Bank == HarmonyBank).Sum(fill => fill.Sounds.Count);
        int effects = recipe.Fills.Where(fill => fill.Bank == EffectsBank).Sum(fill => fill.Sounds.Count);
        bool hasBass = recipe.Fills.Any(fill => fill.Bank == KitBank && fill.Lane == Colour && fill.Sounds.Any(sound => sound.Level == Bass));

        List<string> parts =
        [
            $"{draft.Profile.Summary}",
            $"Bank A: drums from the {kitName} kit (and similar kits for what it lacks), one instrument per column{(hasBass ? $", with bass notes in {draft.Key.Name} at the top right" : string.Empty)}.",
            loops > 0 ? $"Bank B: {loops} loop{(loops == 1 ? string.Empty : "s")} at {draft.Tempo} BPM that fit the key." : "Bank B: empty (the Sound Bank has no loops at this tempo).",
            harmony > 0 ? $"Bank C: {harmony} chords, notes and bass sounds in {draft.Key.Name}." : "Bank C: empty.",
            effects > 0 ? $"Bank D: {effects} effects, vocal shots and textures." : "Bank D: empty.",
            "The sequencer has four patterns: the groove, a variation, a breakdown and a fill.",
        ];
        return string.Join(' ', parts);
    }

    private static LaunchpadRecipe Compose(LaunchpadGenerationRequest request, StyleProfile profile, Pools pools, int seed)
    {
        Random random = new(seed);
        int tempo = ChooseTempo(profile, pools, random, request.Tempo);
        Key key = ChooseKey(profile, pools, random, tempo, request.Key);
        string kit = ChooseKit(profile, pools, random, request.Kit);
        int swing = random.Next(profile.Swing.Min, profile.Swing.Max + 1);

        Draft draft = new(pools, profile, random, key, tempo, kit);
        FillKitBank(draft);
        FillLoopBank(draft);
        FillHarmonyBank(draft);
        FillEffectsBank(draft);

        List<RecipeTrack> tracks = [new(0, KitBank, Low), new(1, KitBank, Backbeat), new(2, KitBank, Hats), ExtraTrack(draft)];
        List<RecipeMix> mixes = [new(Accent, Send: 0.25), new(Hats, Pan: 0.2), new(Cymbals, Pan: -0.2), new(Percussion, Pan: -0.25), new(Toms, Pan: 0.15)];

        LaunchpadRecipe recipe = new()
        {
            Title = $"{profile.Name} · {tempo} BPM · {key.Name}",
            StyleKey = profile.Key,
            Seed = seed,
            Tempo = tempo,
            SwingLevel = swing,
            RootPitchClass = key.Tonic.Value,
            ScaleIndex = LaunchpadScale.IndexOf(key.Scale),
            Kit = kit,
            Fills = draft.Fills,
            Mixes = mixes,
            Tracks = tracks,
            Patterns = Patterns(draft, Math.Clamp(request.Energy, 0, 1)),
        };
        return recipe with { Description = Describe(draft, recipe) };
    }

    private static Pools PoolsOf(SampleIndex index) => _pools.GetValue(index, i => new Pools(i));
    #endregion

    #region Public methods
    ///<summary>
    ///Generates a setup for <paramref name="request"/>. If a draft fails <see cref="LaunchpadQuality"/>, the next seed is tried, up
    ///to <see cref="MaxAttempts"/> times; the recipe records the seed that worked.
    ///</summary>
    ///<exception cref="KeyNotFoundException">There is no such style.</exception>
    ///<exception cref="InvalidOperationException">No seed gave a setup that passes the checks.</exception>
    public static LaunchpadRecipe Generate(LaunchpadGenerationRequest request, SampleIndex index)
    {
        StyleProfile profile = StyleProfiles.Get(request.StyleKey);
        Pools pools = PoolsOf(index);
        IReadOnlyList<string> problems = [];
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            int seed = unchecked(request.Seed + (attempt * SeedStride));
            LaunchpadRecipe recipe = Compose(request, profile, pools, seed);
            problems = LaunchpadQuality.Check(recipe, index);
            if (problems.Count == 0)
            {
                return recipe;
            }
        }

        throw new InvalidOperationException($"Couldn't generate a {profile.Name} setup that passes the checks: {string.Join("; ", problems)}");
    }

    ///<summary>
    ///The recipe of a ready-made preset (see <see cref="StyleProfile.Presets"/>).
    ///</summary>
    ///<exception cref="ArgumentException">There is no such preset.</exception>
    public static LaunchpadRecipe Preset(string presetId, SampleIndex index)
    {
        (StyleProfile profile, StylePreset preset) = StyleProfiles.Presets.FirstOrDefault(pair => pair.Preset.Id == presetId);
        if (preset is null)
        {
            throw new ArgumentException($"There is no Launchpad preset called '{presetId}'.", nameof(presetId));
        }

        LaunchpadRecipe recipe = Generate(new LaunchpadGenerationRequest(profile.Key, preset.Seed, preset.Tempo, preset.Key, preset.Kit), index);
        return recipe with { Title = $"{preset.Name} · {recipe.Tempo} BPM · {recipe.Key.Name}" };
    }

    ///<summary>
    ///The ready-made presets, for the Projects menu.
    ///</summary>
    public static IReadOnlyList<LaunchpadExampleInfo> Presets { get; } =
    [
        .. StyleProfiles.Presets.Select(pair => new LaunchpadExampleInfo(
            pair.Preset.Id,
            $"{pair.Preset.Name} · {pair.Preset.Tempo.ToString(CultureInfo.InvariantCulture)} BPM · {Key.Parse(pair.Preset.Key).Name}",
            $"{pair.Profile.Summary} Drums from the {(Taxonomies.Kits.TryGet(pair.Preset.Kit, out KitConcept? kit) ? kit.DisplayName : pair.Preset.Kit)} kit, loops at the tempo, and chords, notes and bass in the key, all chosen from the Sound Bank by the generator.",
            pair.Profile.Key)),
    ];

    ///<summary>
    ///The styles the generator knows, for the Projects menu.
    ///</summary>
    public static IReadOnlyList<LaunchpadStyleInfo> Styles { get; } =
        [.. StyleProfiles.All.Select(profile => new LaunchpadStyleInfo(profile.Key, profile.Name, profile.Summary, profile.Tempo.Min, profile.Tempo.Max))];
    #endregion
}
