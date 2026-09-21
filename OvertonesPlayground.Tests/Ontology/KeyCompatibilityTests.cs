using OvertonesPlayground.Ontology.Theory;

namespace OvertonesPlayground.Tests.Ontology;

///<summary>
///Whether real samples of the bank can sound in a key: chords by their triad, notes by their pitch, loops strictly by their key,
///and drums never.
///</summary>
public sealed class KeyCompatibilityTests
{
    #region Private methods
    private static Sample Named(string name) => RealCatalog.Index.Find(name + ".wav") ?? throw new InvalidOperationException($"No sample '{name}'.");
    #endregion

    #region Public methods
    [Theory]
    [InlineData("Organ Pad LDre Amin7", "E minor", KeyFit.Fits)]
    [InlineData("Organ Pad LDre Amin7", "C major", KeyFit.Fits)]
    [InlineData("Organ Pad LDre Amin7", "E major", KeyFit.Clashes)]
    [InlineData("E-Piano LDre BbMaj7", "F major", KeyFit.Fits)]
    [InlineData("E-Piano LDre BbMaj7", "E minor", KeyFit.Clashes)]
    public void Judge_AChordOneShot_FitsWhenItsTriadIsInTheKey(string name, string key, KeyFit expected) =>
        Assert.Equal(expected, KeyCompatibility.Judge(Named(name), Key.Parse(key)));

    [Theory]
    [InlineData("Mallet Guitar E2", "C major", KeyFit.Fits)]
    [InlineData("Mallet Guitar Bb2", "C major", KeyFit.Clashes)]
    [InlineData("Mallet Guitar Bb2", "F major", KeyFit.Fits)]
    public void Judge_ANoteOneShot_FitsWhenTheNoteIsInTheKey(string name, string key, KeyFit expected) =>
        Assert.Equal(expected, KeyCompatibility.Judge(Named(name), Key.Parse(key)));

    [Theory]
    [InlineData("Grand Piano Dirty Stabs E Minor 90 bpm", "E minor", KeyFit.Fits)]
    [InlineData("Grand Piano Dirty Stabs E Minor 90 bpm", "G major", KeyFit.Fits)]
    [InlineData("Grand Piano Dirty Stabs E Minor 90 bpm", "A minor", KeyFit.Clashes)]
    public void Judge_AMinorLoop_FitsOnlyItsOwnKeyOrItsRelativeMajor(string name, string key, KeyFit expected) =>
        Assert.Equal(expected, KeyCompatibility.Judge(Named(name), Key.Parse(key)));

    [Fact]
    public void Judge_DrumsNamedLikeNotes_AreNeutral()
    {
        Sample snare = Named("Snare DMX A1");

        Assert.Equal(KeyFit.Neutral, KeyCompatibility.Judge(snare, Key.Parse("Eb major")));
        Assert.Null(KeyCompatibility.PitchOf(snare));
    }

    [Fact]
    public void Judge_EveryDrumOneShot_IsNeutralInEveryKey()
    {
        List<Sample> drums = [.. RealCatalog.Index.All.Where(sample => KeyCompatibility.IsDrum(sample) && !KeyCompatibility.IsPassage(sample))];

        Assert.NotEmpty(drums);
        Assert.All(drums, drum => Assert.Equal(KeyFit.Neutral, KeyCompatibility.Judge(drum, Key.Parse("F# major"))));
    }

    [Fact]
    public void PitchOf_ReadsTheChordRootNamedKeyOrMeasuredPitch()
    {
        Assert.Equal(9, KeyCompatibility.PitchOf(Named("Organ Pad LDre Amin7"))!.Value.Value);
        Assert.Equal(4, KeyCompatibility.PitchOf(Named("Mallet Guitar E2"))!.Value.Value);
    }

    [Fact]
    public void IsMelodic_TellsInstrumentsWithNotesFromDrums()
    {
        Assert.True(KeyCompatibility.IsMelodic(Named("Mallet Guitar E2")));
        Assert.True(KeyCompatibility.IsMelodic(Named("Organ Pad LDre Amin7")));
        Assert.False(KeyCompatibility.IsMelodic(Named("Snare DMX A1")));
    }
    #endregion
}
