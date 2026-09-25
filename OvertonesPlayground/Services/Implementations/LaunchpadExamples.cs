using OvertonesPlayground.Models;
using OvertonesPlayground.Ontology.Model;
using OvertonesPlayground.Ontology.Querying;
using static OvertonesPlayground.Services.Implementations.ExampleLevel;
using static OvertonesPlayground.Services.Implementations.LaunchpadExampleBuilder;

namespace OvertonesPlayground.Services.Implementations;

///<summary>
///The ready-made Launchpad setups. Each is a recipe over the sound bank rather than a list of file names for the drums: the
///sounds are chosen by what the ontology knows about them, so a setup keeps working when the bank grows. Every setup follows the
///same rules, and the tests hold each one to them:
///<list type="bullet">
///<item>one instrument role per column (a lane), the same in every bank, so the column mixer means the same thing everywhere;</item>
///<item>drums from one kit, with a second kit only for what the first lacks;</item>
///<item>centre-of-the-mix sounds (kick, snare, bass) that survive being summed to mono;</item>
///<item>kicks that leave room for an 808 bass instead of stacking a second sub-bass on it;</item>
///<item>every pad brought to the level of its role, so the mix is balanced before anyone touches a fader;</item>
///<item>loops at exactly the setup's tempo and in its key, never a loop that would drift or clash;</item>
///<item>Radio on, so a column of loops launches one at a time and a closed hat cuts an open one;</item>
///<item>echo only on a lane that never holds a loop: the echo plays a second copy of the pad a beat later, which on a loop would
///layer a delayed duplicate of the whole loop.</item>
///</list>
///</summary>
public static class LaunchpadExamples
{
    #region Constants
    ///<summary>Id of the boom-bap setup.</summary>
    public const string BoomBapId = "boom-bap";

    ///<summary>Id of the trap setup.</summary>
    public const string TrapId = "trap";

    ///<summary>Id of the club setup.</summary>
    public const string ClubId = "club";

    private const int KitBank = 0;
    private const int LoopBank = 1;
    private const int MinorScale = 1;
    private const int RootC = 0;
    private const int RootE = 4;
    private const int RootF = 5;
    #endregion

    #region Fields
    ///<summary>The hand-percussion instrument names gathered into the Percussion column and scored when picking a kit.</summary>
    private static readonly string[] HandPercussionInstruments = ["shaker", "tambourine", "cabasa"];
    #endregion

    #region Private methods
    ///<summary>
    ///Assembles one setup. The tests use this to look at where every sample went.
    ///</summary>
    internal static LaunchpadExampleBuilder Assemble(string id, SampleIndex index, Func<Sample, string> pathOf) => id switch
    {
        BoomBapId => BoomBap(index, pathOf),
        TrapId => Trap(index, pathOf),
        ClubId => Club(index, pathOf),
        _ => throw new ArgumentException($"There is no Launchpad example called '{id}'.", nameof(id)),
    };

    ///<summary>
    ///A swung 90 BPM hip-hop setup in E minor: the Vinyl kit, 808 bass notes in the key, and break loops.
    ///</summary>
    private static LaunchpadExampleBuilder BoomBap(SampleIndex index, Func<Sample, string> pathOf)
    {
        LaunchpadExampleBuilder b = new(index, pathOf, tempo: 90, swingLevel: 3, scaleIndex: MinorScale);

        // Bank A: the Vinyl kit, a column per role. The 808 sits in the low end, so the kicks are the ones with the least sub-bass.
        b.Fill(KitBank, Low, Kick, Choose(b.Solid("kick", "vinyl").Where(k => Sub(k) <= MaxKickSubWithBass && Duration(k) <= 0.5), 8, Centroid));
        b.Fill(KitBank, Backbeat, Snare, Choose(b.Solid("snare", "vinyl").Where(s => Duration(s) <= 0.5), 8, Centroid));
        b.Fill(KitBank, Accent, Tag(b.Solid("clap", "vinyl").OrderBy(Centroid), Clap).Concat(Tag(b.Solid("rim", "vinyl").OrderBy(Centroid), Rim)));
        b.Fill(KitBank, Hats, Hat, Interleave(Choose(b.Solid("hihat-closed", "vinyl"), 4, Centroid), Choose(b.Solid("hihat-open", "vinyl"), 4, Centroid)));
        b.Fill(KitBank, Percussion, HandPercussion, [.. Choose(b.Solid("shaker", "vinyl"), 4, Centroid), .. Choose(b.Solid("cowbell", "vinyl"), 2, Centroid), .. Choose(b.Solid("woodblock", "vinyl"), 2, Centroid)]);
        b.Fill(KitBank, Toms, Tom, b.Solid("tom", "vinyl").OrderBy(Centroid));
        b.Fill(KitBank, Colour, Bass, b.BassNotes([RootE, 7, 2]).OrderBy(s => s.EffectivePitchClass == RootE ? 0 : 1).ThenBy(s => s.Id, StringComparer.Ordinal));

        // Bank B: loops at exactly 90 BPM, and only in E minor.
        b.Fill(LoopBank, Backbeat, Beat, [b.Loop("Break Ghosts 90 bpm", RootE, true), b.Loop("Hip Hop Shuffle Dry 90 bpm", RootE, true), b.Loop("80s Beat 90 bpm", RootE, true)], loop: true);
        b.Fill(LoopBank, Colour, Music, [b.Loop("Grand Piano Dirty Stabs E Minor 90 bpm", RootE, true)], loop: true);
        b.Fill(LoopBank, Toms, Texture, [b.Named("Crackle Vinyl Pop")], loop: true);

        b.Mix(Accent, send: 0.25);
        b.Mix(Hats, pan: 0.2);
        b.Mix(Cymbals, pan: -0.2);
        b.Mix(Percussion, pan: -0.25);
        b.Mix(Toms, pan: 0.15);

        b.Track(0, KitBank, Low);
        b.Track(1, KitBank, Backbeat);
        b.Track(2, KitBank, Hats);
        b.Track(3, KitBank, Colour);

        // The groove: kick and snare on the boom-bap spots, swung eighth-note hats with ghost notes, the 808 locked to the kick.
        b.Pattern(0, (0, "X.....x...X....."), (1, "....X.......X..?"), (2, "X.o.x.o?X.o.x.o?"), (3, "X.........x....."));
        b.Pattern(1, (0, "X..?..x..X..?..."), (1, "....X.......X.-x"), (2, "XoxoXoxoXoxoXoxo"), (3, "X.........x....."));
        b.Pattern(2, (0, "X.......x......."), (1, "........X......."), (2, "X...o...x...o..."), (3, "X..............."));
        return b;
    }

    ///<summary>
    ///A 140 BPM half-time trap setup in C minor: a short kick from the Vinyl kit under the 808 kit's snares, claps and hats, with
    ///808 bass notes on the root and the fifth.
    ///</summary>
    private static LaunchpadExampleBuilder Trap(SampleIndex index, Func<Sample, string> pathOf)
    {
        LaunchpadExampleBuilder b = new(index, pathOf, tempo: 140, swingLevel: 0, scaleIndex: MinorScale);

        // The 808 kit's own kicks are long sub-bass tails, so a second sub-bass under the 808 note would only muddy it. The kick
        // comes from a second kit that has short kicks with little below 60 Hz, and it brings its hats too (the 808 kit has almost
        // none); the 808 kit supplies everything else. Vinyl is left to the boom-bap setup.
        string kickKit = CompanionKit(b, "808", avoid: ["vinyl"], needsKick: true, needsClap: false);
        b.Fill(KitBank, Low, Kick, Choose(TightKicks(b, kickKit), 8, Centroid));
        b.Fill(KitBank, Backbeat, Tag(b.Solid("clap", "808").Where(c => Duration(c) <= 1.0).OrderBy(Centroid), Clap).Concat(Tag(Choose(b.Solid("snare", "808").Where(s => Duration(s) <= 0.5), 6, Centroid), Snare)));
        b.Fill(KitBank, Accent, Tag(b.Solid("rim", "808").OrderBy(Centroid), Rim).Concat(Tag(b.Solid("cowbell", "808").OrderBy(Centroid), HandPercussion)));
        b.Fill(KitBank, Hats, Hat, Interleave(Choose(b.Solid("hihat-closed", kickKit).Where(h => Duration(h) <= 0.3), 4, Centroid), Choose(b.Solid("hihat-open", kickKit), 4, Centroid)));
        b.Fill(KitBank, Cymbals, Hat, Choose(b.Solid("cymbal", "808").Where(c => Duration(c) <= 5), 6, Centroid));
        b.Fill(KitBank, Percussion, HandPercussion, b.Solid("shaker", "808").Concat(b.Solid("conga", "808")).OrderBy(Centroid));
        b.Fill(KitBank, Toms, Tom, Choose(b.Solid("tom", "808"), 8, Centroid));

        // Bass notes on the root (C) and the fifth (G): the notes that never clash in a minor key.
        List<Sample> roots = [.. b.BassNotes([RootC]).OrderBy(s => s.Id, StringComparer.Ordinal)];
        b.Fill(KitBank, Colour, Bass, [.. Choose(roots, 6, Duration), .. b.BassNotes([7])]);

        // Bank B: loops at exactly 140 BPM.
        b.Fill(LoopBank, Backbeat, Beat, [b.Loop("Trap Tastic 140 bpm", RootC, true)], loop: true);
        b.Fill(LoopBank, Hats, Tops, [b.Loop("Hihat Triplet Hybrid 140 bpm", RootC, true)], loop: true);
        b.Fill(LoopBank, Cymbals, Tops, [b.Loop("Ride 140 bpm", RootC, true)], loop: true);
        b.Fill(LoopBank, Percussion, HandPercussion, [b.Loop("Outer Bongos 140bpm", RootC, true)], loop: true);
        b.Fill(LoopBank, Toms, Music, [b.Loop("Space Paddy C 140 bpm", RootC, true)], loop: true);

        b.Mix(Accent, send: 0.25);
        b.Mix(Hats, pan: 0.2);
        b.Mix(Cymbals, pan: -0.2);
        b.Mix(Percussion, pan: -0.25);
        b.Mix(Toms, pan: 0.15);

        b.Track(0, KitBank, Low);
        b.Track(1, KitBank, Backbeat);
        b.Track(2, KitBank, Hats);
        b.Track(3, KitBank, Colour);

        // Half-time: the clap lands on the third beat, the hats run in sixteenths and roll at the end of the bar, and the 808
        // note is written on the kick.
        b.Pattern(0, (0, "X......x..x....."), (1, "........X......."), (2, "XoxoXoxoXoxoX?x?"), (3, "X......x..x....."));
        b.Pattern(1, (0, "X.....xX..x..?.."), (1, "........X.....?x"), (2, "X.x.X.x.X.x.XoXo"), (3, "X......x..x..?.."));
        b.Pattern(2, (0, "X..............."), (1, "........X......."), (2, "o...o...o...o..."), (3, "X..............."));
        return b;
    }

    ///<summary>
    ///A 130 BPM four-on-the-floor setup in F minor: 909 kick, snare and toms with a second kit's hats and claps, and loops in
    ///F minor.
    ///</summary>
    private static LaunchpadExampleBuilder Club(SampleIndex index, Func<Sample, string> pathOf)
    {
        LaunchpadExampleBuilder b = new(index, pathOf, tempo: 130, swingLevel: 0, scaleIndex: MinorScale);
        const string primary = "909";

        // The 909 kit has no hats and a single clap, so a second kit that completes it (closed and open hats, a clap and hand
        // percussion) supplies those; nothing is taken from a third. Vinyl is left to the boom-bap setup, so the two do not share
        // their hats.
        string companion = CompanionKit(b, primary, avoid: ["vinyl"], needsKick: false, needsClap: true);

        b.Fill(KitBank, Low, Kick, Choose(b.Solid("kick", primary).Where(k => Duration(k) <= 0.8), 8, Centroid));
        b.Fill(KitBank, Backbeat, Snare, Choose(b.Solid("snare", primary).Where(s => Duration(s) <= 0.6), 8, Centroid));
        b.Fill(KitBank, Accent, Tag(b.Solid("clap", companion).OrderBy(Centroid), Clap).Concat(Tag(b.Solid("rim", primary).OrderBy(Centroid), Rim)));
        b.Fill(KitBank, Hats, Hat, Interleave(Choose(b.Solid("hihat-closed", companion), 4, Centroid), Choose(b.Solid("hihat-open", companion), 4, Centroid)));
        b.Fill(KitBank, Cymbals, Hat, Choose(b.Solid("crash", companion).Concat(b.Solid("ride", companion)), 6, Centroid));
        b.Fill(KitBank, Percussion, HandPercussion, Choose(HandPercussionInstruments.SelectMany(i => b.Solid(i, companion)), 8, Centroid));
        b.Fill(KitBank, Toms, Tom, Choose(b.Solid("tom", primary), 8, Centroid));
        b.Fill(KitBank, Colour, Fx, [b.Named("Stinger F 128 bpm"), b.Named("Impact Layered"), b.Named("Riser Synth"), b.Named("Riser White Noise")]);

        // Bank B: loops at exactly 130 BPM, in F minor. "Mood Atmos E 130 bpm" is at the right tempo but a semitone from the key,
        // so it is not here; "Atmos Colluding D# 130 bpm" is (D sharp is the flat seventh of F minor).
        b.Fill(LoopBank, Backbeat, Beat, [b.Loop("Lava Beat 130 bpm", RootF, true), b.Loop("UK Club Tikka 130 bpm", RootF, true), b.Loop("Dark Garage 130 bpm", RootF, true)], loop: true);
        b.Fill(LoopBank, Colour, Music, [b.Loop("Chugged Arp Fmin 130 bpm", RootF, true), b.Loop("Stutter Fmin 130 bpm", RootF, true), b.Loop("Reptile F 130 bpm", RootF, true)], loop: true);
        b.Fill(LoopBank, Hats, Tops, [b.Loop("Hihat Convergence 130 bpm", RootF, true), b.Loop("Hihat Electronic Essentials 130 bpm", RootF, true)], loop: true);
        b.Fill(LoopBank, Cymbals, Tops, [b.Loop("Noise Blaze 130 bpm", RootF, true)], loop: true);
        b.Fill(LoopBank, Toms, Texture, [b.Loop("Atmos Calm 130 bpm", RootF, true), b.Loop("Atmos Colluding D# 130 bpm", RootF, true)], loop: true);

        b.Mix(Accent, send: 0.25);
        b.Mix(Hats, pan: 0.2);
        b.Mix(Cymbals, pan: -0.2);
        b.Mix(Percussion, pan: -0.25);
        b.Mix(Toms, pan: 0.15);

        b.Track(0, KitBank, Low);
        b.Track(1, KitBank, Accent);
        b.Track(2, KitBank, Hats);
        b.Track(3, KitBank, Percussion);

        // Four on the floor, the clap on two and four, off-beat hats, and a shaker in sixteenths.
        b.Pattern(0, (0, "X...X...X...X..."), (1, "....X.......X..."), (2, "..X...X...X...X."), (3, "x.o.x.o.x.o.x.o?"));
        b.Pattern(1, (0, "X...X...X...X.x?"), (1, "....X.......X..x"), (2, "..X...X...X...XX"), (3, "xoxoxoxoxoxoxox?"));
        b.Pattern(2, (0, "X..............."), (1, "........X......."), (2, "..o...o...o...o."), (3, "o...o...o...o..."));
        return b;
    }

    ///<summary>
    ///Short kicks of a kit that leave room for a bass under them: brief, and with little of their energy below 60 Hz.
    ///</summary>
    private static IEnumerable<Sample> TightKicks(LaunchpadExampleBuilder builder, string kit) =>
        builder.Solid("kick", kit).Where(kick => Sub(kick) <= MaxKickSubWithBass && Duration(kick) <= 0.4);

    ///<summary>
    ///The kit, other than <paramref name="primaryKit"/> and those to <paramref name="avoid"/>, that best supplies what the primary
    ///lacks. It must have at least three closed hats and two open ones, and, if asked, a clap and at least three tight kicks; the
    ///one with the most of these (and hand percussion) wins.
    ///</summary>
    private static string CompanionKit(LaunchpadExampleBuilder builder, string primaryKit, string[] avoid, bool needsKick, bool needsClap)
    {
        string? best = null;
        int bestScore = -1;
        foreach (string kit in builder.KitKeys.Where(key => key != primaryKit && !avoid.Contains(key)))
        {
            int closed = builder.Solid("hihat-closed", kit).Count();
            int open = builder.Solid("hihat-open", kit).Count();
            int claps = builder.Solid("clap", kit).Count();
            int kicks = TightKicks(builder, kit).Count();
            bool isComplete = closed >= 3 && open >= 2 && (!needsClap || claps >= 1) && (!needsKick || kicks >= 3);
            if (!isComplete)
            {
                continue;
            }

            int hand = HandPercussionInstruments.Sum(instrument => builder.Solid(instrument, kit).Count());
            int score = Math.Min(closed, 4) + Math.Min(open, 4) + Math.Min(claps, 2) + Math.Min(hand, 4) + (needsKick ? Math.Min(kicks, 4) : 0);
            if (score > bestScore)
            {
                best = kit;
                bestScore = score;
            }
        }

        return best ?? throw new InvalidOperationException($"No kit other than '{primaryKit}' has the hats {(needsClap ? "and a clap " : string.Empty)}{(needsKick ? "and short kicks " : string.Empty)}to complete it.");
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Builds the setup <paramref name="id"/> from the sound bank, giving every pad and track the path <paramref name="pathOf"/>
    ///returns for its sample.
    ///</summary>
    ///<exception cref="ArgumentException">There is no such setup.</exception>
    ///<exception cref="InvalidOperationException">The bank does not have the sounds the setup needs.</exception>
    public static LaunchpadProject Build(string id, SampleIndex index, Func<Sample, string> pathOf) => Assemble(id, index, pathOf).Project;
    #endregion

    #region Public properties
    ///<summary>
    ///The setups that can be built.
    ///</summary>
    public static IReadOnlyList<LaunchpadExampleInfo> All { get; } =
    [
        new(
            BoomBapId,
            "Boom Bap · 90 BPM · E minor",
            "Bank A is the Vinyl drum kit, one instrument to a column: the kicks are the ones with the least sub-bass, so they stay clear of the 808 bass notes in E, G and D at the top right; closed and open hats share a column, and Radio lets one cut the other. Bank B has 90 BPM break loops, a piano stab loop in E minor and vinyl crackle, each column launching one loop at a time. The sequencer plays a swung groove in three patterns. Tip: play an 808 pad, then use Note mode for bass lines in E minor."),
        new(
            TrapId,
            "808 Trap · 140 BPM · C minor",
            "Bank A: a short kick with little sub-bass (so it doesn't fight the 808) and tight hats from a second kit, under the 808 kit's claps, snares, rims, cymbals and toms, with 808 bass notes on C and G. Bank B: 140 BPM loops (trap beat, triplet hats, ride, bongos and a pad in C). The sequencer plays a half-time pattern with the clap on the third beat and rolling hats. Tip: play an 808 pad, then use Note mode."),
        new(
            ClubId,
            "Dark Club · 130 BPM · F minor",
            "Bank A: 909 kicks, snares and toms with a second kit's hats, claps and shakers, and effects in the top right. Bank B: 130 BPM loops (club beats, hats, arps and stabs in F minor, and two atmospheres that fit the key). The sequencer plays four on the floor with off-beat hats. Loops that are at 130 BPM but in a clashing key are left out."),
    ];
    #endregion
}
