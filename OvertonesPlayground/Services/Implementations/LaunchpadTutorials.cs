using OvertonesPlayground.Models;
using static OvertonesPlayground.Models.LaunchpadControl;
using static OvertonesPlayground.Models.LaunchpadTutorialFocus;

namespace OvertonesPlayground.Services.Implementations;

///<summary>
///The Launchpad's guided tutorials. Each is a list of steps: a tip that points at a button or pad, says what it is for and why,
///and then (on "Show me") lets the app do it on the real Launchpad with sounds from the Sound Bank, so the user watches the pads
///light up and hears the result. Together they cover every button, and the ideas the buttons are for:
///<list type="number">
///<item>Meet the pads: sounds, timbre in a column, banks, loops, edit mode and picking sounds from the Sound Bank.</item>
///<item>Rhythm: a beat built step by step in the sequencer.</item>
///<item>Timing: tempo, tap tempo, the click, swing and fixed length.</item>
///<item>Feel: velocity, probability, micro timing, pattern settings, mutation and undo.</item>
///<item>Playing live: quantise, record arm, capture and print to clip.</item>
///<item>Melody: notes, scales, transposing and playing one tune on different sounds.</item>
///<item>Harmony: chords, triads, sevenths and suspended chords, major and minor, held chords and melody over harmony.</item>
///<item>Timbre: the same note on five instruments, attack and decay, and speed.</item>
///<item>Mixing: layers, volume, pan, mute and solo, radio, echo, master controls, stop clip, setup and projects.</item>
///</list>
///The pad numbers are the pad's place in the grid, counting along each row from the top left (0 to 63), and refer to the setups in
///<see cref="LaunchpadLessons"/>: the drum kit's kick is pad 56, its snare 57, and so on.
///</summary>
public static class LaunchpadTutorials
{
    #region Constants
    ///<summary>Id of the tutorial on pads, banks and loops.</summary>
    public const string PadsId = "pads";

    ///<summary>Id of the tutorial on building a beat.</summary>
    public const string RhythmId = "rhythm";

    ///<summary>Id of the tutorial on tempo and swing.</summary>
    public const string TimingId = "timing";

    ///<summary>Id of the tutorial on accents and variation.</summary>
    public const string FeelId = "feel";

    ///<summary>Id of the tutorial on quantise and capture.</summary>
    public const string LiveId = "live";

    ///<summary>Id of the tutorial on melody.</summary>
    public const string MelodyId = "melody";

    ///<summary>Id of the tutorial on harmony.</summary>
    public const string HarmonyId = "harmony";

    ///<summary>Id of the tutorial on timbre.</summary>
    public const string TimbreId = "timbre";

    ///<summary>Id of the tutorial on mixing.</summary>
    public const string MixId = "mix";

    private const int Major = 0;
    private const int Minor = 1;
    private const int Pentatonic = 2;
    #endregion

    #region Fields
    // The pads that play "Ode to Joy" in Note mode in C major (D is pad 0, E 1, F 2, G 3, and the root C is pad 15).
    private static readonly int[] _odePhrase = [1, 1, 2, 3, 3, 2, 1, 0];
    private static readonly int[] _odeTune = [1, 1, 2, 3, 3, 2, 1, 0, 15, 15, 0, 1, 1, 0, 0];

    // The eight hi-hat steps of a bar of eighth notes.
    private static readonly int[] _eighths = [0, 2, 4, 6, 8, 10, 12, 14];

    // The pads of the bottom row of the keys setup: the same C3 on five instruments.
    private static readonly int[] _sameNote = [57, 58, 59, 60, 61];

    // I, V, vi, IV in C major on the bottom row of Chord mode.
    private static readonly int[] _progression = [63, 60, 61, 59];

    // The four sequencer tracks pressed in turn (ending back on the first) to demonstrate track selection.
    private static readonly int[] _trackTour = [0, 1, 2, 3, 0];

    // The three columns armed in the recording tutorial.
    private static readonly int[] _recordColumns = [0, 1, 3];
    #endregion

    #region Private methods
    private static Task Press(ILaunchpadTutorialHost host, LaunchpadControl control) => host.PressAsync(control);

    private static Task Shifted(ILaunchpadTutorialHost host, LaunchpadControl control) => host.PressAsync(control, shifted: true);

    private static LaunchpadTutorialStep Step(string title, string message, LaunchpadTutorialFocus focus, Func<ILaunchpadTutorialHost, Task>? perform = null, string? result = null) =>
        new(title, message, focus, perform, result);

    private static LaunchpadTutorial Pads() => new(
        PadsId,
        "1. Meet the pads",
        "Sounds, banks, loops, and putting Sound Bank sounds on pads.",
        LaunchpadLessons.GrooveId,
        [
            Step(
                "Sixty-four pads",
                "Every pad plays a sound. Bank A now holds a drum kit from the Sound Bank, one kind of sound per column, from the kick on the left to an 808 bass on the right.",
                OnKey(Session, Row(7))),
            Step(
                "Tap to play",
                "A tap plays a pad, and sounds placed in time make rhythm. Watch the kick, hi-hat and snare pads as I play them.",
                OnKey(Session, 56, 57, 59),
                async host => await host.TapAsync([56, 59, 57, 59, 56, 56, 57, 59], 280),
                "Kick, hat, snare, hat: that pattern of sounds in time is a rhythm. Tap the pads yourself, then press Next."),
            Step(
                "Same sound, new timbre",
                "Each pad up a column is the same kind of sound with a different timbre, or tone colour. These are different kicks: some darker, some brighter, some shorter.",
                OnPad(0, Column(0)),
                async host => await host.TapAsync([56, 48, 40, 32, 24, 16, 8, 0], 450)),
            Step(
                "Banks",
                "Left and Right flip between four banks, A to D, of 64 pads each. Let's go to bank B.",
                OnKeys(Right, [Left]),
                async host => await Press(host, Right)),
            Step(
                "Loops",
                "Bank B has loops at 90 BPM: drum breaks, a piano stab and vinyl crackle. A loop repeats until it is stopped. Radio is on, so a new pad in a column replaces the one playing.",
                OnKey(Session, 57, 49, 41, 63, 62),
                async host =>
                {
                    await host.TapAsync([57], 2900);
                    await host.TapAsync([63], 2900);
                    await host.TapAsync([62], 2900);
                },
                "Three loops, all at 90 BPM, layer into one track. Next, stop them."),
            Step(
                "Stop everything",
                "The square in the top bar stops every sound at once. Stop Clip, at the bottom right, stops just one column.",
                new LaunchpadTutorialFocus { Toolbar = LaunchpadToolbarItem.StopAll, Keys = [StopClip] },
                async host => await host.StopAllAsync()),
            Step(
                "Edit mode",
                "The pencil turns on edit mode. A tap on a pad then opens its menu instead of playing it: assign a sound, loop it, stop it or clear it. Pressing and holding a pad opens the same menu.",
                OnToolbar(LaunchpadToolbarItem.Edit),
                async host =>
                {
                    await host.SetEditModeAsync(true);
                    await host.WaitAsync(600);
                },
                "Every pad shows a pencil. Tap one to open its menu."),
            Step(
                "Sounds from the Sound Bank",
                "Assign sample from Sound Bank lets you search and audition all 2,220 sounds, then puts your choice on the pad. I'll do it for this empty pad with a cowbell.",
                OnPad(60),
                async host =>
                {
                    await host.AssignFromSoundBankAsync(60, "Cowbell 808 DMX");
                    await host.SetEditModeAsync(false);
                    await host.TapAsync([60], 900);
                },
                "The cowbell is on the pad. The same menu can also import a sound from your device."),
            Step(
                "Loop a pad",
                "Loop makes a pad repeat until it is stopped, so even a single hit becomes part of a groove.",
                OnPad(60),
                async host =>
                {
                    await host.ToggleLoopAsync(60);
                    await host.TapAsync([60], 3200);
                    await host.StopAllAsync();
                },
                "The cowbell kept going until it was stopped. A looping pad is marked 'loop'."),
            Step(
                "The eye",
                "The eye hides the card of hints floating over the pads, for a clear view of the grid. That's the basics. Next up: build a beat in Rhythm.",
                OnToolbar(LaunchpadToolbarItem.ShowHideInfo)),
        ]);

    private static LaunchpadTutorial Rhythm() => new(
        RhythmId,
        "2. Rhythm: build a beat",
        "Kick, snare, hi-hats and bass in the step sequencer, then patterns.",
        LaunchpadLessons.GrooveId,
        [
            Step(
                "The sequencer",
                "Rhythm is when sounds happen. Sequencer mode turns the pads into a timeline: read the top rows like text. Sixteen steps make one bar, and four steps make one beat.",
                OnKey(Sequencer, Rows(0, 1)),
                async host => await Press(host, Sequencer),
                "The top rows are steps. The bottom four rows choose each track's sound."),
            Step(
                "Four tracks",
                "The sequencer has four tracks, each playing one sound: kick, snare, hi-hat and 808 bass. The buttons under the pads choose the track, and a pad in the bottom rows swaps its sound.",
                OnTrack(0, [1, 2, 3], Rows(4, 7)),
                async host =>
                {
                    foreach (int track in _trackTour)
                    {
                        await host.PressTrackAsync(track);
                        await host.WaitAsync(450);
                    }
                }),
            Step(
                "Kick on 1 and 3",
                "Steps is where you switch steps on. A kick on step 1 and step 9 lands on beats 1 and 3 of the bar.",
                OnKey(Steps, 0, 8),
                async host =>
                {
                    await host.PressTrackAsync(0);
                    await Press(host, Steps);
                    await host.TapAsync([0, 8], 550);
                },
                "A lit pad is a step that will sound. Each tap plays its sound so you can hear what you place."),
            Step(
                "The backbeat",
                "Track 2 is the snare. On steps 5 and 13, beats 2 and 4, it makes the backbeat you would clap along to.",
                OnTrack(1, [], 4, 12),
                async host =>
                {
                    await host.PressTrackAsync(1);
                    await host.TapAsync([4, 12], 550);
                }),
            Step(
                "Hi-hats in eighths",
                "Track 3, the hi-hat, on every other step plays eighth notes. Their steady ticking keeps time and adds energy.",
                OnTrack(2, [], _eighths),
                async host =>
                {
                    await host.PressTrackAsync(2);
                    await host.TapAsync(_eighths, 150);
                }),
            Step(
                "Play it",
                "Play walks along the steps and loops back to the start. The moving light shows where you are in the bar.",
                OnKey(Play, Rows(0, 1)),
                async host =>
                {
                    await Press(host, Play);
                    await host.WaitAsync(6000);
                },
                "One bar, looping. It keeps playing while we change things."),
            Step(
                "Add a bass line",
                "Track 4 plays an 808 bass note from the Sound Bank. Kick and bass together give a beat its low end. Watch it join while the loop plays.",
                OnTrack(3, [], 0, 8),
                async host =>
                {
                    await host.PressTrackAsync(3);
                    await host.TapAsync([0, 8], 450);
                    await host.WaitAsync(4000);
                }),
            Step(
                "Move a hit",
                "Clear removes whatever you tap next. Moving the kick and the bass from step 9 to step 11 pushes the beat forward: an off-beat placement that makes a groove feel alive.",
                OnKey(Clear, 8, 10),
                async host =>
                {
                    await Press(host, Clear);
                    await host.PressTrackAsync(0);
                    await host.TapAsync([8], 300);
                    await host.PressTrackAsync(3);
                    await host.TapAsync([8], 300);
                    await Press(host, Clear);
                    await host.PressTrackAsync(0);
                    await host.TapAsync([10], 300);
                    await host.PressTrackAsync(3);
                    await host.TapAsync([10], 300);
                    await host.WaitAsync(3500);
                },
                "Same sounds, new placement. Rhythm is as much where the hits are as what they are."),
            Step(
                "Eight patterns",
                "You have been editing pattern 1. There are eight, one for each variation: a fill, a break, a new section. Let's look at them.",
                OnKey(Patterns, Row(0)),
                async host =>
                {
                    await Press(host, Patterns);
                    await host.TapAsync([1], 300);
                    await host.WaitAsync(2500);
                },
                "Pattern 2 is empty, so the loop went quiet. Next: copy pattern 1 onto it."),
            Step(
                "Copy a pattern",
                "Duplicate copies: tap the pattern to copy, then the one to copy onto. It is the quickest way to start a variation.",
                OnKey(Duplicate, 0, 1),
                async host =>
                {
                    await Press(host, Duplicate);
                    await host.TapAsync([0, 1], 500);
                    await Press(host, Duplicate);
                    await host.WaitAsync(3000);
                },
                "Pattern 2 now plays the same beat. Change it as you like; pattern 1 is untouched."),
            Step(
                "Double",
                "Shift plus Duplicate is Double. It repeats the pattern to fill twice the length, 16 steps becoming 32, ready for a second bar that you can vary.",
                OnKeys(Duplicate, [Shift], Rows(0, 3)),
                async host =>
                {
                    await Press(host, Steps);
                    await Shifted(host, Duplicate);
                    await host.WaitAsync(3500);
                },
                "Two bars now: steps 17 to 32 are a copy of the first bar. Change the second bar's steps to make it different."),
            Step(
                "Stop and recap",
                "Press Play again to stop. You have built a beat: kick, snare, hi-hats and bass, placed in time.",
                OnKey(Play),
                async host => await Press(host, Play),
                "Stopped. You built a whole beat from four sounds, one step at a time. Next: Timing."),
        ]);

    private static LaunchpadTutorial Timing() => new(
        TimingId,
        "3. Timing: tempo and swing",
        "Tempo, tap tempo, the metronome, swing and fixed length.",
        LaunchpadLessons.BeatId,
        [
            Step(
                "Timing",
                "Timing is how sounds sit in time: how fast, how loose, how tight. Here is the beat from the Rhythm tutorial. I'll start it.",
                OnKey(Play, Rows(0, 1)),
                async host =>
                {
                    await Press(host, Sequencer);
                    await Press(host, Play);
                    await host.WaitAsync(3000);
                },
                "It plays at 90 BPM: 90 beats a minute."),
            Step(
                "Tempo",
                "Shift plus Device is Tempo. The pads count up 2 BPM at a time, from 60 at the top left to 186 at the bottom right. Watch it speed up, then settle.",
                OnKeys(LaunchpadControl.Device, [Shift], 0, 15, 30, 45, 63),
                async host =>
                {
                    await Shifted(host, LaunchpadControl.Device);
                    await host.TapAsync([30], 4200);
                    await host.TapAsync([45], 3500);
                    await host.TapAsync([15], 500);
                    await Shifted(host, LaunchpadControl.Device);
                },
                "120, then 150, then back to 90. Faster tempo feels more urgent; slower feels heavier."),
            Step(
                "Tap tempo",
                "Shift plus Sends is Tap tempo: tap it along with a song and the tempo follows you. Four taps half a second apart make 120 BPM.",
                OnKeys(Sends, [Shift]),
                async host =>
                {
                    for (int tap = 0; tap < 4; tap++)
                    {
                        await Shifted(host, Sends);
                        await host.WaitAsync(250);
                    }

                    await host.WaitAsync(3000);
                },
                "The tempo followed the taps, to about 120 BPM. Tap it to match any song."),
            Step(
                "The click",
                "Shift plus Solo is Click, a metronome: a higher tick on the first beat of each bar and softer ticks on the rest, so you can hear the grid the music sits on.",
                OnKeys(Solo, [Shift]),
                async host =>
                {
                    await Shifted(host, Solo);
                    await host.WaitAsync(4500);
                    await Shifted(host, Solo);
                }),
            Step(
                "Swing",
                "Shift plus Stop Clip is Swing. It delays every second step, turning a straight, robotic feel into a shuffle. The eight columns of pads are eight amounts, from none on the left to the most on the right.",
                OnKeys(StopClip, [Shift], Row(0)),
                async host =>
                {
                    await Shifted(host, StopClip);
                    await host.TapAsync([0], 3400);
                    await host.TapAsync([3], 3400);
                    await host.TapAsync([7], 3400);
                    await host.TapAsync([3], 300);
                    await Shifted(host, StopClip);
                },
                "Same notes, different feel: none, then some, then a lot. Around 3 is where boom bap lives."),
            Step(
                "Fixed length",
                "Fixed Length cuts every sound off after a set time: one beat, two, four or eight. It tightens long, ringing sounds so they cannot blur the beat.",
                OnKey(FixedLength),
                async host =>
                {
                    await Press(host, FixedLength);
                    await host.WaitAsync(3200);
                    for (int press = 0; press < 4; press++)
                    {
                        await Press(host, FixedLength);
                    }

                    await host.WaitAsync(500);
                },
                "Every sound was cut short after one beat, then Fixed Length cycled back to off. Next: Feel."),
        ]);

    private static LaunchpadTutorial Feel() => new(
        FeelId,
        "4. Feel: accents and surprises",
        "Velocity, probability, micro timing, pattern settings, mutation and undo.",
        LaunchpadLessons.BeatId,
        [
            Step(
                "Velocity",
                "Velocity is how hard a step hits. Tap a lit step to step it down: full, three quarters, half, a quarter. Softening the hi-hats between the beats makes a pattern sound played, not programmed.",
                OnKey(Velocity, 2, 6, 10, 14),
                async host =>
                {
                    await Press(host, Sequencer);
                    await Press(host, Play);
                    await host.WaitAsync(2000);
                    await host.PressTrackAsync(2);
                    await Press(host, Velocity);
                    await host.TapAsync([2, 6, 10, 14, 2, 6, 10, 14], 220);
                    await host.WaitAsync(3500);
                    await Press(host, Velocity);
                },
                "The hats on the beat stayed strong and the ones between got softer: a bounce."),
            Step(
                "Probability",
                "Probability is the chance a step plays. Tap a lit step to lower it. A step at 50 percent plays about half the time, so the pattern never repeats exactly, like a drummer who varies their playing.",
                OnKey(Probability, 10, 14),
                async host =>
                {
                    await host.PressTrackAsync(2);
                    await Press(host, Probability);
                    await host.TapAsync([10, 10, 14, 14], 220);
                    await host.WaitAsync(5500);
                    await Press(host, Probability);
                },
                "Listen over a few bars: those two hats come and go."),
            Step(
                "Micro step",
                "Micro Step nudges a step late by a quarter, half or three quarters of a step. A snare that lands just behind the beat feels relaxed and heavy, the lazy feel of a lot of hip-hop.",
                OnKey(MicroStep, 12),
                async host =>
                {
                    await host.PressTrackAsync(1);
                    await Press(host, MicroStep);
                    await host.TapAsync([12, 12], 300);
                    await host.WaitAsync(4500);
                    await Press(host, MicroStep);
                },
                "The second snare now lands half a step late. Small shifts in timing change the whole attitude."),
            Step(
                "Pattern settings",
                "Pattern Settings changes the pattern itself. The top row sets its length (4 steps per pad), the second row its direction, and the third row its speed.",
                OnKey(PatternSettings, [.. Row(0), 8, 9, 10, 11, 16, 17, 18]),
                async host =>
                {
                    await Press(host, PatternSettings);
                    await host.TapAsync([1], 3500);
                    await host.TapAsync([3], 300);
                    await host.TapAsync([9], 3500);
                    await host.TapAsync([8], 300);
                    await host.TapAsync([18], 3000);
                    await host.TapAsync([17], 300);
                    await Press(host, PatternSettings);
                },
                "A shorter loop, then the same beat backwards, then twice as fast. All are back to normal."),
            Step(
                "Mutation",
                "Mutation randomly flips some steps and changes some velocities. It's a quick source of new ideas, and if you don't like the result, Undo brings the pattern back.",
                OnKey(Mutation),
                async host =>
                {
                    await Press(host, Steps);
                    await Press(host, Mutation);
                    await host.WaitAsync(3500);
                    await Press(host, Mutation);
                    await host.WaitAsync(3500);
                },
                "The beat has drifted from what you built. Next: how to get it back."),
            Step(
                "Undo",
                "Shift plus Record Arm is Undo. It steps back through your edits one at a time, so it's safe to experiment.",
                OnKeys(RecordArm, [Shift]),
                async host =>
                {
                    await Shifted(host, RecordArm);
                    await Shifted(host, RecordArm);
                    await host.WaitAsync(3500);
                    await Press(host, Play);
                },
                "Two undos took back the two mutations. Next: play live."),
        ]);

    private static LaunchpadTutorial Live() => new(
        LiveId,
        "5. Play it live: quantise and capture",
        "Tight timing from loose taps, and turning what you play into a pattern.",
        LaunchpadLessons.BeatId,
        [
            Step(
                "Quantise",
                "Quantise makes pads you play live wait for the next step, so slightly sloppy taps land exactly in time. Hear the hi-hat pad tapped loosely, then with Quantise on.",
                OnKey(Quantise, 59),
                async host =>
                {
                    await Press(host, Sequencer);
                    await Press(host, Play);
                    await host.WaitAsync(1500);
                    await Press(host, Session);
                    await host.TapAsync([59, 59, 59, 59, 59, 59], 210);
                    await host.WaitAsync(800);
                    await Press(host, Quantise);
                    await host.TapAsync([59, 59, 59, 59, 59, 59], 210);
                    await host.WaitAsync(1500);
                    await Press(host, Quantise);
                },
                "First the taps wandered off the grid; then each waited for the next step and snapped to it. Quantise is now off again."),
            Step(
                "Record arm",
                "Record Arm chooses which columns Capture will listen to. Press it, then the buttons under the pads for the kick, snare and hi-hat columns: 1, 2 and 4.",
                OnTrack(0, [1, 3], Row(7)),
                async host =>
                {
                    await Press(host, RecordArm);
                    foreach (int column in _recordColumns)
                    {
                        await host.PressTrackAsync(column);
                        await host.WaitAsync(300);
                    }

                    await Press(host, RecordArm);
                },
                "Those three columns are armed. Now only pads in them will be captured."),
            Step(
                "Capture",
                "Capture turns the last two bars you played into a pattern. Shift plus Quantise, Record Quantise, snaps what it captures to whole steps. I'll stop the sequencer, play a groove on the pads, then capture it.",
                OnKeys(Capture, [Quantise, Shift]),
                async host =>
                {
                    await Press(host, Play);
                    await Press(host, Session);
                    await Shifted(host, Quantise);
                    await host.TapAsync([56, 59, 56, 57, 59, 56, 59, 57], 340);
                    await Press(host, Capture);
                    await Press(host, Play);
                    await host.WaitAsync(5500);
                },
                "Your playing became a pattern in the sequencer: press Play to hear it any time. It lands on the beat because Record Quantise is on."),
            Step(
                "Print to clip",
                "Print to Clip renders the pattern, with its tempo, swing and levels, into a new audio clip in your library, ready to trim, layer or export. Try it on a beat you like.",
                OnKey(PrintToClip),
                async host => await Press(host, Play),
                "The sequencer is stopped. That's playing live: Quantise for tight timing, Capture to keep what you play. Next: Melody."),
        ]);

    private static LaunchpadTutorial Melody() => new(
        MelodyId,
        "6. Melody",
        "Notes, scales, transposing, and one tune on different sounds.",
        LaunchpadLessons.KeysId,
        [
            Step(
                "Notes make melody",
                "A melody is notes that rise and fall in time. Bank A's second column is a mallet guitar from the Sound Bank: each pad is the same instrument at a different pitch, C, E, G, then C an octave up.",
                OnKey(Session, 57, 49, 41, 33),
                async host => await host.TapAsync([57, 49, 41, 33, 41, 49, 57], 350),
                "Low to high and back: that's the shape of a melody. But here each pad is a separate recording."),
            Step(
                "Note mode",
                "Note mode needs just one sound. It plays the last pad you tapped, faster or slower, to make every other note. First I'll tap the mallet C, then switch to Note.",
                OnKey(Note, Rows(0, 1)),
                async host =>
                {
                    await host.TapAsync([57], 300);
                    await Press(host, Note);
                },
                "Two rows, two octaves: notes climb left to right along the second row, then carry on along the top row."),
            Step(
                "A scale",
                "A scale is a set of notes that sound good together. Major is bright and happy. Pad 15, at the right end of the second row, is the root, C. Listen from low to high.",
                OnPad(15, Rows(0, 1)),
                async host => await host.TapAsync([8, 9, 10, 11, 12, 13, 14, 15, 0, 1, 2, 3, 4, 5, 6], 190),
                "That's the C major scale: do, re, mi, fa, sol, la, ti, do, and on up."),
            Step(
                "Play a tune",
                "Melodies are shapes that repeat. Here is 'Ode to Joy': it moves by steps along the scale, so it needs only five neighbouring pads.",
                OnPad(1, 0, 1, 2, 3, 15),
                async host => await host.TapAsync(_odeTune, 360),
                "Fifteen notes from five pads. Small steps make a smooth tune; big jumps make it dramatic."),
            Step(
                "Change the scale",
                "Setup lets you choose the scale for Note and Chord modes. Minor sounds serious or sad. Pentatonic has just five notes, so there are no wrong ones. Blues adds a 'blue note'. Chromatic has all twelve.",
                OnKey(Setup, Rows(0, 1)),
                async host =>
                {
                    await host.SetScaleAsync(Minor);
                    await host.TapAsync(_odePhrase, 360);
                    await host.WaitAsync(500);
                    await host.SetScaleAsync(Pentatonic);
                    await host.TapAsync([10, 11, 12, 13, 14, 15, 0, 1, 2, 3, 4], 220);
                    await host.SetScaleAsync(Major);
                },
                "Same pads, different notes: minor changed the mood, and pentatonic made every pad fit. The scale is back to major."),
            Step(
                "Transpose",
                "Up and Down shift every pad by a semitone: a new key, same tune. Higher feels brighter and lighter, lower feels heavier.",
                OnKeys(Up, [Down]),
                async host =>
                {
                    await Press(host, Up);
                    await Press(host, Up);
                    await host.TapAsync(_odePhrase, 300);
                    await host.WaitAsync(600);
                    for (int press = 0; press < 4; press++)
                    {
                        await Press(host, Down);
                    }

                    await host.TapAsync(_odePhrase, 300);
                    await host.WaitAsync(600);
                    await Press(host, Up);
                    await Press(host, Up);
                },
                "Two semitones up, then two down: one shape in three keys. Transpose is back to zero."),
            Step(
                "Same tune, new sound",
                "Note mode plays whichever pad you tapped last, so tapping a different instrument first gives the same melody in a new voice. Here it is on the organ, then on plucked strings.",
                OnKey(Session, 59, 61),
                async host =>
                {
                    await Press(host, Session);
                    await host.TapAsync([59], 300);
                    await Press(host, Note);
                    await host.TapAsync(_odePhrase, 320);
                    await host.WaitAsync(700);
                    await Press(host, Session);
                    await host.TapAsync([61], 300);
                    await Press(host, Note);
                    await host.TapAsync(_odePhrase, 320);
                },
                "One melody in three voices. What makes them different is timbre: see the Timbre tutorial. Next: Harmony."),
        ]);

    private static LaunchpadTutorial Harmony() => new(
        HarmonyId,
        "7. Harmony",
        "Chords, triads, sevenths, suspended chords, major and minor, and melody over harmony.",
        LaunchpadLessons.KeysId,
        [
            Step(
                "Notes together",
                "Harmony is notes sounding together. Bank A's last column holds chords recorded on an organ: Am7, Dm7, Em7 and Fmaj7. Each pad is a whole chord, and all use only the notes of C major.",
                OnKey(Session, 63, 55, 47, 39),
                async host => await host.TapAsync([63, 55, 47, 39], 1900),
                "Four chords, each a stack of notes. In a chord name, m is minor, maj is major, and 7 adds a fourth note."),
            Step(
                "Chord mode",
                "Chord mode builds chords from a single sound, so you are not limited to the recorded ones. First I'll tap the organ's C3 so that Chord mode has a sound to build on.",
                OnKey(Chord, 59),
                async host =>
                {
                    await Press(host, Session);
                    await host.TapAsync([59], 300);
                    await Press(host, Chord);
                    await host.WaitAsync(400);
                },
                "Every pad now plays a chord built on the organ. The columns are the chord's root climbing the scale: the far right is C."),
            Step(
                "Triads",
                "The bottom row is triads: three notes, the root plus every other note of the scale. Play C, G, A minor, F: some of the most used chords in pop music.",
                OnPad(63, Row(7)),
                async host => await host.TapAsync(_progression, 1400),
                "I, V, vi, IV in C major. The mood shifts as the root moves, but it is the same three-note recipe each time."),
            Step(
                "Seventh chords",
                "The row above adds a fourth note, the seventh. Sevenths sound richer, warmer and a little jazzy. Listen to the same four chords with sevenths.",
                OnPad(55, Row(6)),
                async host => await host.TapAsync([55, 52, 53, 51], 1400),
                "C major 7, G7, A minor 7, F major 7: the same progression, with more colour."),
            Step(
                "Suspended chords",
                "The next two rows are suspended chords: the third is swapped for the second (sus2) or the fourth (sus4). They sound open and unresolved, as if asking a question.",
                OnPad(47, [.. Row(5), .. Row(4)]),
                async host => await host.TapAsync([47, 39, 63], 1500),
                "Sus2, then sus4, then the plain C chord answering them: tension and release."),
            Step(
                "Major and minor",
                "The scale decides each chord's mood. In Setup, changing Major to Minor lowers the third of every chord: bright becomes dark, with the very same pads.",
                OnKey(Setup, Row(7)),
                async host =>
                {
                    await host.SetScaleAsync(Major);
                    await host.TapAsync(_progression, 1000);
                    await host.SetScaleAsync(Minor);
                    await host.TapAsync(_progression, 1000);
                    await host.SetScaleAsync(Major);
                },
                "Same pads, new scale: C major became C minor. The third of a chord is the note that makes it sound happy or sad."),
            Step(
                "Hold a chord",
                "Custom mode plays a pad only for as long as you hold it, like an organ key. It is ideal for chords and pads that you want to shape by hand. I'll hold Am7, then Fmaj7.",
                OnKey(Custom, 63, 39),
                async host =>
                {
                    await Press(host, Custom);
                    await host.HoldAsync(63, 2500);
                    await host.WaitAsync(300);
                    await host.HoldAsync(39, 2500);
                },
                "Hold to sound, release to stop. Hold a chord under a melody and you are making music."),
            Step(
                "Melody over harmony",
                "Harmony under a melody is most of music. I'll let a chord ring, then play the melody over it in Note mode.",
                OnKey(Note, 63),
                async host =>
                {
                    await Press(host, Session);
                    await host.TapAsync([63], 400);
                    await host.TapAsync([57], 300);
                    await Press(host, Note);
                    await host.TapAsync(_odePhrase, 380);
                    await host.WaitAsync(1500);
                },
                "A chord underneath and a tune on top: harmony and melody working together. Next: Timbre."),
        ]);

    private static LaunchpadTutorial Timbre() => new(
        TimbreId,
        "8. Timbre",
        "The same note on five instruments, attack and decay, and playback speed.",
        LaunchpadLessons.KeysId,
        [
            Step(
                "Same note, different sound",
                "Timbre is a sound's tone colour: why a guitar and a piano sound different playing the same note. The bottom row has C3 on a mallet guitar, an electric piano, an organ, a muted guitar and plucked strings.",
                OnPad(57, _sameNote),
                async host => await host.TapAsync(_sameNote, 1100),
                "One pitch, five personalities."),
            Step(
                "Attack and decay",
                "Two things shape timbre most. Attack is how a sound starts, and decay is how it fades. The organ swells and holds; the mallet strikes and dies away.",
                OnPad(59, 57, 59),
                async host =>
                {
                    await host.TapAsync([59], 1800);
                    await host.StopAllAsync();
                    await host.TapAsync([57], 1800);
                }),
            Step(
                "Measured in the Sound Bank",
                "Tone colour can be measured. Select any sound in the Sound Bank and it lists its attack, decay and brightness, so you can find a dark, bright, sharp or smooth sound on purpose.",
                OnPad(58, _sameNote)),
            Step(
                "Speed changes colour",
                "Device sets a column's playback speed. Slower makes a sound lower and darker, faster makes it higher and thinner: speed changes pitch and timbre together. Listen to the organ at half, normal and double speed.",
                OnKey(LaunchpadControl.Device, Column(3)),
                async host =>
                {
                    await Press(host, LaunchpadControl.Device);
                    await host.TapAsync([PadAt(7, 3)], 300);
                    await Press(host, LaunchpadControl.Device);
                    await host.TapAsync([59], 1600);
                    await Press(host, LaunchpadControl.Device);
                    await host.TapAsync([PadAt(0, 3)], 300);
                    await Press(host, LaunchpadControl.Device);
                    await host.TapAsync([59], 1600);
                    await Press(host, LaunchpadControl.Device);
                    await host.TapAsync([PadAt(4, 3)], 300);
                    await Press(host, LaunchpadControl.Device);
                    await host.TapAsync([59], 1600);
                },
                "Half speed was an octave lower and duller, double speed an octave higher and thinner, and the column is back to normal."),
            Step(
                "Choose your colours",
                "Choosing timbre is choosing sounds. Any pad can take any sound in the Sound Bank from its menu. I'll put a harpsichord on an empty pad so you can compare it with the others.",
                OnPad(50),
                async host =>
                {
                    await host.AssignFromSoundBankAsync(50, "Harpsichord Pluck C2");
                    await host.TapAsync([50, 57], 1200);
                },
                "The harpsichord sits on its own pad now: tap it, then the mallet guitar, and compare their colours. Next: Mixing."),
        ]);

    private static LaunchpadTutorial Mix() => new(
        MixId,
        "9. Mixing: layers and space",
        "Volume, pan, mute, solo, radio, echo, master controls and projects.",
        LaunchpadLessons.GrooveId,
        [
            Step(
                "Layers",
                "A mix is sounds layered together. Bank B has three loops at 90 BPM, all in time and in key: a drum break, a piano stab and vinyl crackle. Each column is a channel with its own controls.",
                new LaunchpadTutorialFocus { Key = Right, Tracks = [1, 6, 7] },
                async host =>
                {
                    await Press(host, Right);
                    await host.TapAsync([57], 2900);
                    await host.TapAsync([63], 2900);
                    await host.TapAsync([62], 1500);
                },
                "Three layers. Now let's balance and place them."),
            Step(
                "Volume",
                "Volume turns the pads into one fader per column: the higher the pad, the louder. Here I'll pull the drums down, then bring them back up.",
                OnKey(Volume, [.. Column(1), .. Column(7)]),
                async host =>
                {
                    await Press(host, Volume);
                    await host.TapAsync([41], 2500);
                    await host.TapAsync([9], 300);
                    await Press(host, Volume);
                },
                "Quieter drums let the piano through, then back up. Height is loudness."),
            Step(
                "Pan",
                "Pan places a column between the left and right speakers: top is right, bottom is left. Panning gives each sound its own space. Headphones show it best.",
                OnKey(Pan, [.. Column(1), .. Column(7)]),
                async host =>
                {
                    await Press(host, Pan);
                    await host.TapAsync([57, 7], 300);
                    await host.WaitAsync(4000);
                    await Press(host, Pan);
                },
                "Drums to the left, piano to the right. It sounds wider because each has its own place."),
            Step(
                "Mute",
                "Mute silences a column: press it, then the button under the column. Muting one layer at a time is how you hear what each adds. I'll mute the drums, then unmute them.",
                OnKeys(Mute, [], 57),
                async host =>
                {
                    await Press(host, Mute);
                    await host.PressTrackAsync(1);
                    await host.WaitAsync(2500);
                    await host.PressTrackAsync(1);
                    await Press(host, Mute);
                    await host.TapAsync([57], 2500);
                },
                "The break dropped out and came back. A muted column stops what it's playing."),
            Step(
                "Solo",
                "Solo is the opposite: while any column is soloed, only soloed columns sound. I'll solo the piano column, then tap the drums (silent) and the piano (heard).",
                OnKeys(Solo, [], 63),
                async host =>
                {
                    await Press(host, Solo);
                    await host.PressTrackAsync(7);
                    await Press(host, Solo);
                    await host.TapAsync([57], 1200);
                    await host.TapAsync([63], 2500);
                    await Press(host, Solo);
                    await host.PressTrackAsync(7);
                    await Press(host, Solo);
                },
                "Only the soloed column made a sound. Solo is now off."),
            Step(
                "Radio",
                "Shift plus Mute is Radio, like the buttons on an old radio: playing a pad stops the others in its column. It is on here, so a new break replaces the last. I'll switch a break in, then turn Radio off and add another.",
                OnKeys(Mute, [Shift], 57, 49, 41),
                async host =>
                {
                    await host.TapAsync([49], 3000);
                    await Shifted(host, Mute);
                    await host.TapAsync([41], 3000);
                    await Shifted(host, Mute);
                },
                "Radio on: the new break replaced the old one. Radio off: the next one piled on top. Radio is back on now."),
            Step(
                "Echo",
                "Sends adds echo: quieter repeats an eighth note apart. It suits short sounds such as a clap best. I'll go back to bank A, add echo to the clap column, and play one clap.",
                new LaunchpadTutorialFocus { Key = Sends, Tracks = [2] },
                async host =>
                {
                    await host.StopAllAsync();
                    await Press(host, Left);
                    await Press(host, Sends);
                    await host.TapAsync([PadAt(2, 2)], 300);
                    await Press(host, Sends);
                    await host.TapAsync([58], 3000);
                },
                "One clap, three echoes fading away. Echo on a loop would layer a delayed copy of the whole loop."),
            Step(
                "Master controls",
                "Shift plus Volume is the master volume, one fader for everything, and Shift plus Pan is the master pan. Set them last. I'll lower the volume, swing the whole mix right then left, then reset it all.",
                OnKeys(Volume, [Shift, Pan]),
                async host =>
                {
                    await Shifted(host, Volume);
                    await host.TapAsync([PadAt(4, 0)], 300);
                    await Shifted(host, Volume);
                    await host.TapAsync([56, 57, 56, 57], 400);
                    await Shifted(host, Pan);
                    await host.TapAsync([PadAt(0, 0)], 300);
                    await Shifted(host, Pan);
                    await host.TapAsync([56, 57, 56, 57], 400);
                    await Shifted(host, Pan);
                    await host.TapAsync([PadAt(7, 0)], 300);
                    await Shifted(host, Pan);
                    await host.TapAsync([56, 57, 56, 57], 400);
                    await host.ResetMixerAsync();
                },
                "Quieter, then the whole mix right, then left, each in one move. Setup's reset put the mixer back to normal."),
            Step(
                "Stop Clip",
                "Stop Clip stops the sounds in a column, so you can end one loop without cutting the rest. I'll start the break and the piano in bank B, then stop the break, then the piano.",
                new LaunchpadTutorialFocus { Key = StopClip, Tracks = [1, 7] },
                async host =>
                {
                    await Press(host, Right);
                    await host.TapAsync([57, 63], 2500);
                    await Press(host, StopClip);
                    await host.PressTrackAsync(1);
                    await host.WaitAsync(2000);
                    await host.PressTrackAsync(7);
                    await Press(host, StopClip);
                }),
            Step(
                "Setup and Projects",
                "Setup resets the mixer, clears a bank or the sequencer, and chooses the scale. Projects saves your whole Launchpad by name, and opens ready-made setups built from the Sound Bank: Boom Bap, Trap and Club. That is the whole tour. Go make something.",
                OnKeys(Setup, [Projects])),
        ]);
    #endregion

    #region Public properties
    ///<summary>
    ///Every tutorial, in the order they are taught.
    ///</summary>
    public static IReadOnlyList<LaunchpadTutorial> All { get; } =
    [
        Pads(),
        Rhythm(),
        Timing(),
        Feel(),
        Live(),
        Melody(),
        Harmony(),
        Timbre(),
        Mix(),
    ];
    #endregion
}
