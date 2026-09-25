using OvertonesPlayground.Models;
using OvertonesPlayground.Ontology.Model;
using OvertonesPlayground.Ontology.Querying;
using static OvertonesPlayground.Services.Implementations.ExampleLevel;

namespace OvertonesPlayground.Services.Implementations;

///<summary>
///The Launchpad setups the tutorials start from, built from the Sound Bank like the ready-made examples are:
///<list type="bullet">
///<item><b>groove</b>: the boom-bap drum kit and loops (bank A and B), with the sequencer empty, for learning pads and rhythm;</item>
///<item><b>beat</b>: the same, with the beat the rhythm lesson builds already in pattern 1, for learning timing and feel;</item>
///<item><b>keys</b>: pitched instruments in C (single notes, five instruments on the same note, and chord samples), for melody,
///harmony and timbre.</item>
///</list>
///The drum setups reuse the boom-bap recipe, so they keep its rules (one role per column, balanced levels, loops at the setup's
///tempo and in its key). The keys setup names its sounds outright: what matters there is that each is in C.
///</summary>
public static class LaunchpadLessons
{
    #region Constants
    ///<summary>Id of the drum kit and loops with an empty sequencer.</summary>
    public const string GrooveId = "groove";

    ///<summary>Id of the drum kit and loops with a finished beat in pattern 1.</summary>
    public const string BeatId = "beat";

    ///<summary>Id of the pitched instruments in C.</summary>
    public const string KeysId = "keys";

    private const int KeysBank = 0;
    #endregion

    #region Fields
    private static readonly string[] _ids = [GrooveId, BeatId, KeysId];
    #endregion

    #region Private methods
    ///<summary>
    ///Turns on the steps <paramref name="steps"/> of a track, at full velocity and always sounding (as a tapped step is).
    ///</summary>
    private static void On(LaunchpadPattern pattern, int track, params int[] steps)
    {
        foreach (int step in steps)
        {
            pattern.Tracks[track][step].IsOn = true;
        }
    }

    ///<summary>
    ///The boom-bap kit and loops with an empty sequencer, and no swing so that Swing has something to change. With
    ///<paramref name="withBeat"/> pattern 1 holds the beat the rhythm lesson ends with.
    ///</summary>
    private static LaunchpadExampleBuilder Groove(SampleIndex index, Func<Sample, string> pathOf, bool withBeat)
    {
        LaunchpadExampleBuilder b = LaunchpadExamples.Assemble(LaunchpadExamples.BoomBapId, index, pathOf);
        LaunchpadProject project = b.Project;
        project.SwingLevel = 0;
        project.Sequence.Patterns = [.. Enumerable.Range(0, LaunchpadSequence.PatternCount).Select(_ => new LaunchpadPattern())];
        project.Sequence.SelectedPattern = 0;
        if (withBeat)
        {
            LaunchpadPattern pattern = project.Sequence.Patterns[0];
            On(pattern, 0, 0, 10);
            On(pattern, 1, 4, 12);
            On(pattern, 2, 0, 2, 4, 6, 8, 10, 12, 14);
            On(pattern, 3, 0, 10);
        }

        return b;
    }

    ///<summary>
    ///Pitched instruments in C, one instrument to a column so the mixer means something. Bank A: bass notes; a mallet guitar with
    ///the notes of a C chord over two octaves; then the same C3 on an electric piano, an organ, a muted jazz guitar and pizzicato
    ///strings (the bottom row, so the timbre lesson can play them across); a column of C major seventh chords; and a column of organ
    ///chords that use only the notes of C major.
    ///</summary>
    private static LaunchpadExampleBuilder Keys(SampleIndex index, Func<Sample, string> pathOf)
    {
        LaunchpadExampleBuilder b = new(index, pathOf, tempo: 100, swingLevel: 0, scaleIndex: 0);
        b.Project.IsRadioOn = false;

        b.Fill(KeysBank, 0, Bass, [b.Named("Bass Sine C1"), b.Named("Bass Sub Star Trek C1"), b.Named("Bass Electric Taka C")]);
        b.Fill(KeysBank, 1, Music, [b.Named("Mallet Guitar C3"), b.Named("Mallet Guitar E3"), b.Named("Mallet Guitar G3"), b.Named("Mallet Guitar C4"), b.Named("Mallet Guitar C2"), b.Named("Mallet Guitar E2"), b.Named("Mallet Guitar G2"), b.Named("Mallet Guitar C1")]);
        b.Fill(KeysBank, 2, Music, [b.Named("E-Piano Suitcase C3")]);
        b.Fill(KeysBank, 3, Music, [b.Named("Organ Tonewheel C3")]);
        b.Fill(KeysBank, 4, Music, [b.Named("Guitar Jazz Mute C3"), b.Named("Guitar Jazz Mute C2")]);
        b.Fill(KeysBank, 5, Music, [b.Named("Strings Pizz Basic C3"), b.Named("Strings Pizz Oct C3"), b.Named("Strings Pizz Stage C3")]);
        b.Fill(KeysBank, 6, Music, [b.Named("E-Piano CMaj7"), b.Named("Stab Master CMaj7"), b.Named("Guitar Chord Jazz CMaj9"), b.Named("E-Piano Motion CMaj9")]);
        b.Fill(KeysBank, 7, Music, [b.Named("Organ Pad LDre Amin7"), b.Named("Organ Pad LDre Dmin7"), b.Named("Organ Pad LDre Emin7"), b.Named("Organ Pad LDre FMaj7")]);
        return b;
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Builds the lesson setup <paramref name="id"/> from the sound bank, giving every pad and track the path
    ///<paramref name="pathOf"/> returns for its sample.
    ///</summary>
    ///<exception cref="ArgumentException">There is no such setup.</exception>
    ///<exception cref="InvalidOperationException">The bank does not have the sounds the setup needs.</exception>
    public static LaunchpadProject Build(string id, SampleIndex index, Func<Sample, string> pathOf) => Assemble(id, index, pathOf).Project;

    ///<summary>
    ///Assembles a setup. The tests use this to look at where every sample went.
    ///</summary>
    internal static LaunchpadExampleBuilder Assemble(string id, SampleIndex index, Func<Sample, string> pathOf) => id switch
    {
        GrooveId => Groove(index, pathOf, withBeat: false),
        BeatId => Groove(index, pathOf, withBeat: true),
        KeysId => Keys(index, pathOf),
        _ => throw new ArgumentException($"There is no Launchpad lesson setup called '{id}'.", nameof(id)),
    };
    #endregion

    #region Public properties
    ///<summary>
    ///The ids of the lesson setups.
    ///</summary>
    public static IReadOnlyList<string> Ids => _ids;
    #endregion
}
