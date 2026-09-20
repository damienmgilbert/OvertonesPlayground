namespace OvertonesPlayground.Tests.Ontology;

public sealed class StereoAnalyzerTests
{
    private readonly StereoAnalyzer _analyzer = new();

    private StereoFacet Analyze(float[] left, float[] right) => _analyzer.Extract(AudioSynth.Context(AudioSynth.Rate, null, left, right));

    [Fact]
    public void Extract_SingleChannel_IsMono()
    {
        StereoFacet facet = _analyzer.Extract(AudioSynth.Mono(AudioSynth.Sine(440, 0.5)));

        Assert.Equal(StereoImage.Mono, facet.Image);
        Assert.Equal(1, facet.Channels);
    }

    [Fact]
    public void Extract_IdenticalChannels_AreDualMono()
    {
        float[] signal = AudioSynth.Noise(0.5);

        StereoFacet facet = Analyze(signal, [.. signal]);

        Assert.Equal(StereoImage.DualMono, facet.Image);
        Assert.Equal(1.0, facet.Correlation);
        Assert.Equal(0.0, facet.Width);
        Assert.Equal(0.0, facet.MonoCompatibilityDb);
    }

    [Fact]
    public void Extract_InvertedChannels_AreOutOfPhaseAndCancelInMono()
    {
        float[] signal = AudioSynth.Noise(0.5);

        StereoFacet facet = Analyze(signal, AudioSynth.Invert(signal));

        Assert.Equal(StereoImage.OutOfPhase, facet.Image);
        Assert.True(facet.PolarityInverted);
        Assert.Equal(-1.0, facet.Correlation, 0.01);
        Assert.Equal(1.0, facet.Width, 0.01);
        Assert.True(facet.MonoCompatibilityDb < -40.0, $"mono compatibility {facet.MonoCompatibilityDb} dB");
    }

    [Fact]
    public void Extract_IndependentNoise_IsWideWithHalfWidth()
    {
        StereoFacet facet = Analyze(AudioSynth.Noise(1.0, seed: 1), AudioSynth.Noise(1.0, seed: 2));

        Assert.Equal(StereoImage.Wide, facet.Image);
        Assert.True(Math.Abs(facet.Correlation) < 0.1);
        Assert.Equal(0.5, facet.Width, 0.05);
        Assert.Equal(-3.0, facet.MonoCompatibilityDb, 0.5);
    }

    [Fact]
    public void Extract_MostlyCorrelatedChannels_AreNarrow()
    {
        float[] left = AudioSynth.Noise(1.0, seed: 1);
        float[] right = [.. left.Zip(AudioSynth.Noise(1.0, amplitude: 0.05, seed: 2), (a, b) => a + b)];

        StereoFacet facet = Analyze(left, right);

        Assert.Equal(StereoImage.Narrow, facet.Image);
        Assert.True(facet.Correlation > 0.9);
    }

    [Fact]
    public void Extract_SoundOnlyInTheLeftChannel_PansHardLeft()
    {
        StereoFacet facet = Analyze(AudioSynth.Sine(440, 0.5), AudioSynth.Silence(0.5));

        Assert.True(facet.Pan < -0.99);
        Assert.True(facet.BalanceDb > 30.0);
    }

    [Fact]
    public void Extract_BassOutOfPhaseWhileTrebleIsInPhase_ShowsInTheLowBandCorrelationOnly()
    {
        float[] bass = AudioSynth.Sine(60, 1.0, amplitude: 0.4);
        float[] treble = AudioSynth.Sine(4000, 1.0, amplitude: 0.4);
        float[] left = [.. bass.Zip(treble, (b, t) => b + t)];
        float[] right = [.. bass.Zip(treble, (b, t) => -b + t)];

        StereoFacet facet = Analyze(left, right);

        Assert.True(facet.LowBandCorrelation < -0.9, $"low band correlation {facet.LowBandCorrelation}");
        Assert.True(facet.Correlation is > -0.3 and < 0.3, $"overall correlation {facet.Correlation}");
    }

    [Fact]
    public void Extract_Silence_IsTreatedAsNeutral()
    {
        StereoFacet facet = Analyze(AudioSynth.Silence(0.2), AudioSynth.Silence(0.2));

        Assert.Equal(StereoImage.Mono, facet.Image);
        Assert.Equal(0.0, facet.Width);
    }
}
