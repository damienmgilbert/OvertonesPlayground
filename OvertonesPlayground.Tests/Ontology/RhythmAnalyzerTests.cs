namespace OvertonesPlayground.Tests.Ontology;

public sealed class RhythmAnalyzerTests
{
    private readonly RhythmAnalyzer _analyzer = new();

    private RhythmFacet? Analyze(float[] signal, double? namedBpm = null, int sampleRate = AudioSynth.Rate)
    {
        NamedAttributes named = namedBpm is null ? NamedAttributes.Empty("test") : new NamedAttributes(namedBpm, null, KeyMode.None, null, null, "test");
        return _analyzer.Extract(AudioSynth.Context(sampleRate, named, signal));
    }

    [Theory]
    [InlineData(90.0)]
    [InlineData(100.0)]
    [InlineData(120.0)]
    [InlineData(140.0)]
    public void Extract_ClickTrain_FindsTheTempo(double bpm)
    {
        RhythmFacet? facet = Analyze(AudioSynth.ClickTrain(bpm, 8.0));

        Assert.NotNull(facet);
        Assert.NotNull(facet!.DetectedBpm);
        Assert.Equal(bpm, facet.DetectedBpm!.Value, 3.0);
        Assert.True(facet.TempoConfidence > 0.3, $"confidence {facet.TempoConfidence}");
    }

    [Fact]
    public void Extract_ClickTrain_CountsTheOnsets()
    {
        RhythmFacet? facet = Analyze(AudioSynth.ClickTrain(120, 8.0));

        Assert.NotNull(facet);
        Assert.InRange(facet!.OnsetCount, 14, 18);
        Assert.InRange(facet.OnsetsPerSecond, 1.6, 2.4);
    }

    [Fact]
    public void Extract_ClickTrainSpanningWholeBeats_IsLoopLike()
    {
        RhythmFacet? facet = Analyze(AudioSynth.ClickTrain(120, 8.0));

        Assert.NotNull(facet);
        Assert.True(facet!.IsLoopLike);
        Assert.Equal(16.0, facet.Beats!.Value, 1.5);
    }

    [Fact]
    public void Extract_ClickTrainCutMidBeat_IsNotLoopLike()
    {
        RhythmFacet? facet = Analyze(AudioSynth.ClickTrain(120, 7.75));

        Assert.NotNull(facet);
        Assert.False(facet!.IsLoopLike);
    }

    [Theory]
    [InlineData(120.0, true)]
    [InlineData(60.0, true)]
    [InlineData(240.0, true)]
    [InlineData(90.0, true)]
    [InlineData(160.0, true)]
    [InlineData(100.0, false)]
    [InlineData(75.0, false)]
    public void Extract_TempoInTheName_IsComparedAllowingHalfDoubleDottedAndTripletRelations(double named, bool expected)
    {
        RhythmFacet? facet = Analyze(AudioSynth.ClickTrain(120, 8.0), named);

        Assert.NotNull(facet);
        Assert.Equal(expected, facet!.TempoAgreesWithName);
    }

    [Fact]
    public void Extract_NoTempoInTheName_LeavesAgreementUnknown()
    {
        RhythmFacet? facet = Analyze(AudioSynth.ClickTrain(120, 8.0));

        Assert.Null(facet!.TempoAgreesWithName);
    }

    // 5.3333 s is 8 beats at 90 BPM, so the loop-length grid holds 90 (8 beats) and 180 (16 beats) inside 60-210 BPM.
    private const double EightBeatsAt90 = 8.0 * 60.0 / 90.0;

    [Theory]
    [InlineData(89.2, EightBeatsAt90, 90.0)]
    [InlineData(90.7, EightBeatsAt90, 90.0)]
    public void SnapToLoopGrid_AnEstimateNearTheGridTempo_BecomesExact(double detected, double seconds, double expected)
    {
        Assert.Equal(expected, RhythmAnalyzer.SnapToLoopGrid(detected, seconds), 0.001);
    }

    [Fact]
    public void SnapToLoopGrid_TheDoubleOfTheLoopTempo_IsFoldedBackToTheOneNearestTheUsualRange()
    {
        Assert.Equal(90.0, RhythmAnalyzer.SnapToLoopGrid(180.4, EightBeatsAt90), 0.001);
        Assert.Equal(90.0, RhythmAnalyzer.SnapToLoopGrid(45.1, EightBeatsAt90), 0.001);
    }

    [Fact]
    public void SnapToLoopGrid_ATripletFeelPulse_IsReplacedByTheLoopTempo()
    {
        // 120 is 4:3 of 90. The loop holds 10.67 beats at 120, which is no loop at all, so 120 cannot be its tempo.
        Assert.Equal(90.0, RhythmAnalyzer.SnapToLoopGrid(120.0, EightBeatsAt90), 0.001);
        // 60 is 2:3 of 90 (5.33 beats).
        Assert.Equal(90.0, RhythmAnalyzer.SnapToLoopGrid(60.0, EightBeatsAt90), 0.001);
    }

    [Fact]
    public void SnapToLoopGrid_ADottedPulseThatAlsoReadsAsAThreeBarLoop_IsLeftAlone()
    {
        // 135 is 3:2 of 90, but 135 BPM over 5.33 s is exactly 12 beats, a legitimate 3-bar loop; the length cannot tell them apart.
        Assert.Equal(135.0, RhythmAnalyzer.SnapToLoopGrid(135.0, EightBeatsAt90), 0.001);
    }

    [Fact]
    public void SnapToLoopGrid_ALoopWithAWholeNumberOfBeatsThatIsNotAPowerOfTwo_KeepsItsOwnTempo()
    {
        // 8 s at 90 BPM is 12 beats: three bars. 120 would also fit the grid (16 beats), but 90 is a loop already.
        Assert.Equal(90.0, RhythmAnalyzer.SnapToLoopGrid(90.0, 8.0), 0.001);
    }

    [Fact]
    public void SnapToLoopGrid_NothingFitsTheLoopLength_KeepsTheEstimate()
    {
        // 5 s at 100 BPM is 8.33 beats; the nearest grid tempo, 96, is 4 % away.
        Assert.Equal(100.0, RhythmAnalyzer.SnapToLoopGrid(100.0, 5.0), 0.001);
    }

    [Fact]
    public void SnapToLoopGrid_WhenTheLoopFitsAtTwoOctaves_PrefersTheSlowerOneNearest110()
    {
        // 6 s holds 8 beats at 80 BPM and 16 at 160: the name of a loop like this is more often the slow one.
        Assert.Equal(80.0, RhythmAnalyzer.SnapToLoopGrid(160.0, 6.0), 0.001);
        Assert.Equal(80.0, RhythmAnalyzer.SnapToLoopGrid(79.9, 6.0), 0.001);
    }

    [Theory]
    [InlineData(0.0, 6.0)]
    [InlineData(100.0, 0.0)]
    [InlineData(-5.0, 6.0)]
    public void SnapToLoopGrid_NonsenseInput_IsReturnedAsIs(double detected, double seconds)
    {
        Assert.Equal(detected, RhythmAnalyzer.SnapToLoopGrid(detected, seconds));
    }

    [Fact]
    public void Extract_ClickTrainSlightlyOffTheLoopLength_KeepsItsOwnEstimate()
    {
        // 120 BPM for 7.75 s is 15.5 beats, more than 2 % from the 16-beat grid tempo (123.9), so nothing is snapped.
        RhythmFacet? facet = Analyze(AudioSynth.ClickTrain(120, 7.75));

        Assert.NotNull(facet);
        Assert.Equal(120.0, facet!.DetectedBpm!.Value, 3.0);
    }

    [Fact]
    public void Extract_TempoSnappedToTheLoopLength_DoesNotMakeAnOffCutClipALoop()
    {
        // 120 BPM for 7.9 s is 15.8 beats: 1.3 % from the 16-beat grid tempo (121.5), so the tempo is snapped to it, and at 121.5
        // the clip would hold exactly 16 beats. But the clip was not cut on the beat, and the snap must not vouch for itself.
        RhythmFacet? facet = Analyze(AudioSynth.ClickTrain(120, 7.9));

        Assert.NotNull(facet);
        Assert.Equal(60.0 * 16 / 7.9, facet!.DetectedBpm!.Value, 0.05);
        Assert.False(facet.IsLoopLike);
    }

    [Fact]
    public void Extract_ClickTrainOnTheLoopGrid_ReportsTheExactGridTempo()
    {
        RhythmFacet? facet = Analyze(AudioSynth.ClickTrain(90, EightBeatsAt90));

        Assert.NotNull(facet);
        Assert.Equal(90.0, facet!.DetectedBpm!.Value, 0.05);
        Assert.True(facet.IsLoopLike);
        Assert.Equal(8.0, facet.Beats!.Value, 0.05);
    }

    [Fact]
    public void Extract_SoundShorterThanOneAndAHalfSeconds_HasNoRhythm()
    {
        Assert.Null(Analyze(AudioSynth.ClickTrain(120, 1.2)));
    }

    [Fact]
    public void Extract_Silence_HasNoRhythm()
    {
        Assert.Null(Analyze(AudioSynth.Silence(4.0)));
    }

    [Fact]
    public void Extract_SteadyTone_HasNoConfidentTempo()
    {
        RhythmFacet? facet = Analyze(AudioSynth.Sine(440, 6.0));

        Assert.NotNull(facet);
        Assert.True(facet!.OnsetCount <= 2, $"onsets {facet.OnsetCount}, bpm {facet.DetectedBpm}, conf {facet.TempoConfidence}");
        Assert.False(facet.IsLoopLike);
    }
}
