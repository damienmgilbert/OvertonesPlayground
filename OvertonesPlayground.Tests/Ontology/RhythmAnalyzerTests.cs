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
