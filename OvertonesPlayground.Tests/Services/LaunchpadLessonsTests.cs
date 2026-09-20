using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

///<summary>
///Holds the tutorials' setups to what the tutorials say about them, against the real sound bank. The tutorials name pads by number
///("the kick is pad 56", "the organ chords are down the right-hand column"), so what has to be checked is that those pads hold
///those sounds.
///</summary>
public sealed class LaunchpadLessonsTests
{
    #region Private methods
    private static LaunchpadProject Build(string id) => LaunchpadLessons.Build(id, RealCatalog.Index, sample => sample.Id);

    private static string? LabelAt(LaunchpadProject project, int bank, int pad) => project.Pads.SingleOrDefault(candidate => candidate.Bank == bank && candidate.Index == pad)?.Label;

    private static LaunchpadPad? PadAt(LaunchpadProject project, int bank, int pad) => project.Pads.SingleOrDefault(candidate => candidate.Bank == bank && candidate.Index == pad);

    private static int[] StepsOn(LaunchpadPattern pattern, int track) => [.. Enumerable.Range(0, LaunchpadPattern.StepCount).Where(step => pattern.Tracks[track][step].IsOn)];
    #endregion

    #region Public methods
    [Fact]
    public void Ids_ListsTheThreeLessons() => Assert.Equal(["groove", "beat", "keys"], LaunchpadLessons.Ids);

    [Theory]
    [InlineData(LaunchpadLessons.GrooveId)]
    [InlineData(LaunchpadLessons.BeatId)]
    [InlineData(LaunchpadLessons.KeysId)]
    public void Build_EachLesson_BuildsFromTheRealSoundBank(string id)
    {
        LaunchpadProject project = Build(id);

        Assert.NotEmpty(project.Pads);
        Assert.All(project.Pads, pad => Assert.False(string.IsNullOrWhiteSpace(pad.ClipPath)));
    }

    [Fact]
    public void Build_UnknownLesson_Throws() => _ = Assert.Throws<ArgumentException>(() => Build("nope"));

    [Theory]
    [InlineData(LaunchpadLessons.GrooveId)]
    [InlineData(LaunchpadLessons.BeatId)]
    public void Drums_HaveTheKitOnBankAAndLoopsOnBankB(string id)
    {
        LaunchpadProject project = Build(id);

        // The kit: a kick, snare, hi-hat and 808 on the bottom row; the column of kicks the tutorial taps from bottom to top.
        foreach (int pad in new[] { 56, 57, 58, 59, 61, 62, 63 })
        {
            Assert.NotNull(PadAt(project, 0, pad));
        }

        Assert.All(new[] { 56, 48, 40, 32, 24, 16, 8, 0 }, pad => Assert.NotNull(PadAt(project, 0, pad)));
        Assert.Contains("Kick", LabelAt(project, 0, 56), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Snare", LabelAt(project, 0, 57), StringComparison.OrdinalIgnoreCase);

        // The loops: three breaks, a piano stab and vinyl crackle, all looping.
        foreach (int pad in new[] { 57, 49, 41, 63, 62 })
        {
            Assert.True(PadAt(project, 1, pad)?.IsLooping, $"Bank B pad {pad} should be a loop.");
        }

        // The pad the tutorial puts a cowbell on is free in both banks, so is the one for the harpsichord's neighbour.
        Assert.Null(PadAt(project, 0, 60));
        Assert.Null(PadAt(project, 1, 60));
        Assert.Equal(90, project.Tempo);
        Assert.True(project.IsRadioOn);
        Assert.Equal(0, project.SwingLevel);
    }

    [Fact]
    public void Groove_HasAnEmptySequencerWithFourTracks()
    {
        LaunchpadProject project = Build(LaunchpadLessons.GrooveId);

        Assert.All(project.Sequence.Patterns, pattern => Assert.All(Enumerable.Range(0, LaunchpadPattern.TrackCount), track => Assert.Empty(StepsOn(pattern, track))));
        Assert.All(project.Sequence.Tracks, track => Assert.True(track.HasSource));

        // Track 1 to 4 are the kick, snare, hi-hat and 808 on the bottom row, as the rhythm tutorial says.
        Assert.Equal([LabelAt(project, 0, 56), LabelAt(project, 0, 57), LabelAt(project, 0, 59), LabelAt(project, 0, 63)], project.Sequence.Tracks.Select(track => track.Label));
    }

    [Fact]
    public void Beat_HasTheBeatTheRhythmTutorialBuilds()
    {
        LaunchpadPattern pattern = Build(LaunchpadLessons.BeatId).Sequence.Patterns[0];

        Assert.Equal(16, pattern.Length);
        Assert.Equal([0, 10], StepsOn(pattern, 0));
        Assert.Equal([4, 12], StepsOn(pattern, 1));
        Assert.Equal([0, 2, 4, 6, 8, 10, 12, 14], StepsOn(pattern, 2));
        Assert.Equal([0, 10], StepsOn(pattern, 3));

        // Tapped steps are at full velocity and always sound, so the velocity and probability tutorials can step them down.
        Assert.All(Enumerable.Range(0, 4), track => Assert.All(StepsOn(pattern, track), step => Assert.Equal(1.0, pattern.Tracks[track][step].Velocity)));
        Assert.All(Enumerable.Range(0, 4), track => Assert.All(StepsOn(pattern, track), step => Assert.Equal(100, pattern.Tracks[track][step].Probability)));
    }

    [Fact]
    public void Keys_HasTheSameC3OnFiveInstrumentsAcrossTheBottomRow()
    {
        LaunchpadProject project = Build(LaunchpadLessons.KeysId);

        Assert.Equal(["Mallet Guitar C3", "E-Piano Suitcase C3", "Organ Tonewheel C3", "Guitar Jazz Mute C3", "Strings Pizz Basic C3"], new[] { 57, 58, 59, 60, 61 }.Select(pad => LabelAt(project, 0, pad)));
    }

    [Fact]
    public void Keys_HasTheMalletNotesOfACChordUpTheSecondColumn()
    {
        LaunchpadProject project = Build(LaunchpadLessons.KeysId);

        // Bottom to top: C, E, G, then C an octave up: the melody tutorial plays these as an arpeggio.
        Assert.Equal(["Mallet Guitar C3", "Mallet Guitar E3", "Mallet Guitar G3", "Mallet Guitar C4"], new[] { 57, 49, 41, 33 }.Select(pad => LabelAt(project, 0, pad)));
    }

    [Fact]
    public void Keys_HasOrganChordsUsingOnlyTheNotesOfCMajorDownTheLastColumn()
    {
        LaunchpadProject project = Build(LaunchpadLessons.KeysId);

        Assert.Equal(["Organ Pad LDre Amin7", "Organ Pad LDre Dmin7", "Organ Pad LDre Emin7", "Organ Pad LDre FMaj7"], new[] { 63, 55, 47, 39 }.Select(pad => LabelAt(project, 0, pad)));
    }

    [Fact]
    public void Keys_IsInCMajorWithNothingOnThePadTheTimbreTutorialFills()
    {
        LaunchpadProject project = Build(LaunchpadLessons.KeysId);

        Assert.Equal(0, project.ScaleIndex);
        Assert.False(project.IsRadioOn);
        Assert.Null(PadAt(project, 0, 50));
        Assert.All(project.Pads, pad => Assert.Equal(0, pad.Bank));
    }
    #endregion
}
