using OvertonesPlayground.Ontology.Theory;

namespace OvertonesPlayground.Tests.Ontology;

///<summary>
///The music theory core: pitch classes, scales, chords, keys, roman numerals and progressions.
///</summary>
public sealed class TheoryTests
{
    #region Public methods
    [Theory]
    [InlineData("C", 0)]
    [InlineData("F#", 6)]
    [InlineData("Gb", 6)]
    [InlineData("Bb", 10)]
    [InlineData("Cb", 11)]
    [InlineData("E#", 5)]
    [InlineData("D##", 4)]
    [InlineData("a♭", 8)]
    public void PitchClass_TryParse_ReadsLettersAndAnyAccidentals(string text, int expected)
    {
        Assert.True(PitchClass.TryParse(text, out PitchClass pitchClass));
        Assert.Equal(expected, pitchClass.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("H")]
    [InlineData("C3")]
    [InlineData("Cx")]
    public void PitchClass_TryParse_RejectsWhatIsNotANoteName(string text) => Assert.False(PitchClass.TryParse(text, out _));

    [Fact]
    public void PitchClass_Transpose_WrapsRoundTheOctave()
    {
        Assert.Equal(1, new PitchClass(11).Transpose(2).Value);
        Assert.Equal(11, PitchClass.C.Transpose(-1).Value);
        Assert.Equal(7, new PitchClass(0).SemitonesUpTo(new PitchClass(7)));
        Assert.Equal(5, new PitchClass(7).SemitonesUpTo(new PitchClass(0)));
    }

    [Fact]
    public void Scales_TheFirstFive_KeepTheirOrderForSavedProjects() =>
        Assert.Equal(["major", "minor", "pentatonic", "blues", "chromatic"], Scales.All.Take(5).Select(scale => scale.Key));

    [Fact]
    public void Scale_Semitones_ContinuesIntoTheNextOctaveBothWays()
    {
        Assert.Equal(12, Scales.Major.Semitones(7));
        Assert.Equal(14, Scales.Major.Semitones(8));
        Assert.Equal(-1, Scales.Major.Semitones(-1));
        Assert.Equal(1, Scales.Major.DegreeOf(14));
        Assert.Equal(-1, Scales.Major.DegreeOf(1));
    }

    [Theory]
    [InlineData("minor", true)]
    [InlineData("dorian", true)]
    [InlineData("phrygian", true)]
    [InlineData("harmonic-minor", true)]
    [InlineData("major", false)]
    [InlineData("mixolydian", false)]
    [InlineData("lydian", false)]
    public void Scale_IsMinor_FollowsTheThird(string key, bool expected) => Assert.Equal(expected, Scales.Find(key)!.IsMinor);

    [Theory]
    [InlineData("C", 0, ChordQuality.Major)]
    [InlineData("F#m", 6, ChordQuality.Minor)]
    [InlineData("Dmin7", 2, ChordQuality.Minor7)]
    [InlineData("BbMaj7", 10, ChordQuality.Major7)]
    [InlineData("AMaj", 9, ChordQuality.Major)]
    [InlineData("Gsus4", 7, ChordQuality.Sus4)]
    [InlineData("Bm7b5", 11, ChordQuality.HalfDiminished7)]
    [InlineData("Emin11", 4, ChordQuality.Minor11)]
    [InlineData("G7", 7, ChordQuality.Dominant7)]
    [InlineData("CM7", 0, ChordQuality.Major7)]
    public void Chord_TryParse_ReadsLeadSheetAndSampleNameSymbols(string text, int root, ChordQuality quality)
    {
        Assert.True(Chord.TryParse(text, out Chord chord));
        Assert.Equal(root, chord.Root.Value);
        Assert.Equal(quality, chord.Quality);
    }

    [Theory]
    [InlineData("E-Piano LDre BbMaj7", "A#maj7")]
    [InlineData("Organ Pad LDre Amin7", "Am7")]
    [InlineData("Stab Synth Ibiza Cmin", "Cm")]
    public void Chord_TryFindInName_FindsTheChordWord(string name, string expected)
    {
        Assert.True(Chord.TryFindInName(name, out Chord chord));
        Assert.Equal(expected, chord.Name());
    }

    [Theory]
    [InlineData("Stab Visions E")]
    [InlineData("Celeste C6")]
    [InlineData("Mallet Guitar C3")]
    [InlineData("Bass Sub C#0")]
    [InlineData("Kick Vinyl Thud")]
    public void Chord_TryFindInName_IgnoresNotesOctavesAndOrdinaryWords(string name) => Assert.False(Chord.TryFindInName(name, out _));

    [Fact]
    public void Chord_PitchClassesAndTriad_AreTheChordsNotes()
    {
        Chord chord = Chord.Parse("Dm7");

        Assert.Equal([2, 5, 9, 0], chord.PitchClasses.Select(p => p.Value));
        Assert.Equal(ChordQuality.Minor, chord.Triad.Quality);
        Assert.True(chord.IsMinor);
    }

    [Fact]
    public void Key_DiatonicChords_OfCMajor_AreTheSevenChordsOfTheKey()
    {
        Key key = Key.MajorOf(PitchClass.C);

        Assert.Equal(["C", "Dm", "Em", "F", "G", "Am", "Bdim"], key.DiatonicChords().Select(chord => chord.Name()));
        Assert.Equal(["Cmaj7", "Dm7", "Em7", "Fmaj7", "G7", "Am7", "Bm7b5"], key.DiatonicChords(sevenths: true).Select(chord => chord.Name()));
    }

    [Fact]
    public void Key_HarmonicMinor_HasAMajorFifthChord()
    {
        Key key = new(new PitchClass(9), Scales.HarmonicMinor);

        Assert.Equal("E", key.DiatonicChord(4)!.Value.Name());
        Assert.Equal("E7", key.DiatonicChord(4, sevenths: true)!.Value.Name());
    }

    [Theory]
    [InlineData("E minor", 4, "minor")]
    [InlineData("Em", 4, "minor")]
    [InlineData("F#min", 6, "minor")]
    [InlineData("Bb major", 10, "major")]
    [InlineData("C", 0, "major")]
    [InlineData("D dorian", 2, "dorian")]
    [InlineData("Bb harmonic minor", 10, "harmonic-minor")]
    public void Key_TryParse_ReadsKeyNames(string text, int tonic, string scale)
    {
        Assert.True(Key.TryParse(text, out Key? key));
        Assert.Equal(tonic, key!.Tonic.Value);
        Assert.Equal(scale, key.Scale.Key);
    }

    [Fact]
    public void Key_NameAndRelative_UseTheUsualSpellingAndPartner()
    {
        Assert.Equal("Bb major", Key.MajorOf(new PitchClass(10)).Name);
        Assert.Equal("F# minor", Key.MinorOf(new PitchClass(6)).Name);
        Assert.Equal("C major", Key.MinorOf(new PitchClass(9)).Relative.Name);
        Assert.Equal("A minor", Key.MajorOf(PitchClass.C).Relative.Name);
    }

    [Theory]
    [InlineData("I", 0, 0, ChordQuality.Major)]
    [InlineData("iv", 3, 0, ChordQuality.Minor)]
    [InlineData("V7", 4, 0, ChordQuality.Dominant7)]
    [InlineData("ii7", 1, 0, ChordQuality.Minor7)]
    [InlineData("Imaj7", 0, 0, ChordQuality.Major7)]
    [InlineData("bVII", 6, -1, ChordQuality.Major)]
    [InlineData("vii°", 6, 0, ChordQuality.Diminished)]
    [InlineData("iiø7", 1, 0, ChordQuality.HalfDiminished7)]
    [InlineData("VI", 5, 0, ChordQuality.Major)]
    public void RomanNumeral_TryParse_ReadsDegreeAccidentalAndQuality(string text, int degree, int accidental, ChordQuality quality)
    {
        Assert.True(RomanNumeral.TryParse(text, out RomanNumeral? numeral));
        Assert.Equal(new RomanNumeral(degree, accidental, quality), numeral);
        Assert.Equal(text, numeral!.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("VIII")]
    [InlineData("Iv")]
    [InlineData("V13")]
    public void RomanNumeral_TryParse_RejectsWhatIsNotANumeral(string text) => Assert.False(RomanNumeral.TryParse(text, out _));

    [Fact]
    public void Progression_In_PlaysTheNumeralsInTheKeysOwnScale()
    {
        Assert.Equal(["Am", "F", "C", "G"], Progression.Parse("i-VI-III-VII").In(Key.MinorOf(new PitchClass(9))).Select(chord => chord.Name()));
        Assert.Equal(["Dm7", "G7", "Cmaj7"], Progression.Parse("ii7 V7 Imaj7").In(Key.MajorOf(PitchClass.C)).Select(chord => chord.Name()));
        Assert.Equal(["Cm", "Db"], Progression.Parse("i-bII").In(Key.MinorOf(PitchClass.C)).Select(chord => chord.Name(preferFlats: true)));
    }

    [Fact]
    public void Chord_IsIn_AcceptsTheKeysChordsAndRejectsOthers()
    {
        Key eMinor = Key.MinorOf(new PitchClass(4));

        Assert.True(Chord.Parse("Am").IsIn(eMinor));
        Assert.True(Chord.Parse("G").IsIn(eMinor));
        Assert.False(Chord.Parse("F").IsIn(eMinor));
        Assert.False(Chord.Parse("E").IsIn(eMinor));
    }

    [Fact]
    public void Interval_NamesAndConsonance_AreTheUsualOnes()
    {
        Assert.Equal("perfect fifth", Interval.Name(19));
        Assert.True(Interval.IsConsonant(Interval.MajorThird));
        Assert.True(Interval.IsHarsh(Interval.Tritone));
        Assert.False(Interval.IsConsonant(Interval.MinorSecond));
    }
    #endregion
}
