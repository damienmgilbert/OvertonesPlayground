namespace OvertonesPlayground.Tests.Ontology;

public sealed class TonalityAnalyzerTests
{
    #region Fields
    private readonly TonalityAnalyzer _analyzer = new();
    #endregion

    #region Private methods
    private TonalityFacet Analyze(float[] signal, int sampleRate = AudioSynth.Rate)
    {
        AnalysisContext context = AudioSynth.Context(sampleRate, null, signal);
        context.Dynamics = new DynamicsAnalyzer().Extract(context);
        context.Spectral = new SpectralAnalyzer().Extract(context);
        return _analyzer.Extract(context);
    }
    #endregion

    #region Public methods
    [Fact]
    public void Extract_HarmonicTone_ReportsTheFundamentalNotAnOvertoneAndIsHarmonic()
    {
        TonalityFacet facet = Analyze(AudioSynth.Harmonic(110, 8, 1.0));

        Assert.NotNull(facet.FundamentalHz);
        Assert.Equal(110.0, facet.FundamentalHz!.Value, 2.0);
        Assert.True(facet.Character.HasFlag(TonalCharacter.Harmonic));
        Assert.True(facet.Inharmonicity < 0.02, $"inharmonicity {facet.Inharmonicity}");
    }

    [Fact]
    public void Extract_Silence_IsUnpitchedWithoutThrowing()
    {
        TonalityFacet facet = Analyze(AudioSynth.Silence(0.5));

        Assert.Null(facet.FundamentalHz);
        Assert.Equal(TonalCharacter.Unpitched, facet.Character);
    }

    [Theory]
    [InlineData(55.0)]
    [InlineData(110.0)]
    [InlineData(440.0)]
    [InlineData(1000.0)]
    [InlineData(2500.0)]
    public void Extract_SineAcrossTheRange_FindsThePitch(double frequencyHz)
    {
        TonalityFacet facet = Analyze(AudioSynth.Sine(frequencyHz, 1.0));

        Assert.NotNull(facet.FundamentalHz);
        Assert.Equal(frequencyHz, facet.FundamentalHz!.Value, frequencyHz * 0.01);
    }

    [Fact]
    public void Extract_SineAt220Hz_FindsThePitchAndNamesTheNote()
    {
        TonalityFacet facet = Analyze(AudioSynth.Sine(220, 1.0));

        Assert.NotNull(facet.FundamentalHz);
        Assert.Equal(220.0, facet.FundamentalHz!.Value, 1.0);
        Assert.True(facet.PitchConfidence > 0.95, $"confidence {facet.PitchConfidence}");
        Assert.Equal("A3", facet.NoteName);
        Assert.Equal(9, facet.PitchClass);
        Assert.True(Math.Abs(facet.CentsOffset!.Value) < 10.0);
        Assert.True(facet.Character.HasFlag(TonalCharacter.Pitched));
        Assert.False(facet.Character.HasFlag(TonalCharacter.Unpitched));
    }

    [Fact]
    public void Extract_SineAt96Khz_FindsThePitchAfterDecimation()
    {
        TonalityFacet facet = Analyze(AudioSynth.Sine(440, 1.0, sampleRate: 96000), 96000);

        Assert.NotNull(facet.FundamentalHz);
        Assert.Equal(440.0, facet.FundamentalHz!.Value, 3.0);
    }

    [Fact]
    public void Extract_SoundTooShortToMeasure_IsUnpitched()
    {
        TonalityFacet facet = Analyze(AudioSynth.Sine(440, 0.02));

        Assert.Null(facet.FundamentalHz);
    }

    [Fact]
    public void Extract_ToneLowAndHigh_AreDarkAndBrightRespectively()
    {
        TonalityFacet low = Analyze(AudioSynth.Sine(300, 1.0));
        TonalityFacet high = Analyze(AudioSynth.Sine(5000, 1.0));

        Assert.True(low.Character.HasFlag(TonalCharacter.Dark));
        Assert.False(low.Character.HasFlag(TonalCharacter.Bright));
        Assert.True(high.Character.HasFlag(TonalCharacter.Bright));
        Assert.False(high.Character.HasFlag(TonalCharacter.Dark));
    }

    [Fact]
    public void Extract_TuneOfAKickDrum_IsFoundInTheDecayingTone()
    {
        float[] kick = AudioSynth.Decay(AudioSynth.Sine(60, 0.6, amplitude: 0.9), 60);

        TonalityFacet facet = Analyze(kick);

        Assert.NotNull(facet.FundamentalHz);
        Assert.Equal(60.0, facet.FundamentalHz!.Value, 3.0);
        Assert.True(facet.Character.HasFlag(TonalCharacter.Sub));
    }

    [Fact]
    public void Extract_WhiteNoise_IsUnpitchedAndNoisy()
    {
        TonalityFacet facet = Analyze(AudioSynth.Noise(1.0));

        Assert.Null(facet.FundamentalHz);
        Assert.True(facet.Character.HasFlag(TonalCharacter.Unpitched));
        Assert.True(facet.Character.HasFlag(TonalCharacter.Noisy));
        Assert.False(facet.Character.HasFlag(TonalCharacter.Pitched));
    }
    #endregion
}
