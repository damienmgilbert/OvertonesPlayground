namespace OvertonesPlayground.Tests.Ontology;

public sealed class SpectralAnalyzerTests
{
    private readonly SpectralAnalyzer _analyzer = new();

    private SpectralFacet Analyze(float[] signal, int sampleRate = AudioSynth.Rate) =>
        _analyzer.Extract(AudioSynth.Context(sampleRate, null, signal));

    [Fact]
    public void Extract_PureTone_PutsTheCentroidOnTheToneAndIsNotFlat()
    {
        SpectralFacet facet = Analyze(AudioSynth.Sine(1000, 1.0));

        Assert.Equal(1000.0, facet.CentroidHz, 40.0);
        Assert.True(facet.Flatness < 0.1, $"flatness {facet.Flatness}");
        Assert.Equal(FrequencyBand.Mid, facet.Bands.Dominant);
    }

    [Theory]
    [InlineData(40.0, FrequencyBand.Sub)]
    [InlineData(100.0, FrequencyBand.Bass)]
    [InlineData(350.0, FrequencyBand.LowMid)]
    [InlineData(1000.0, FrequencyBand.Mid)]
    [InlineData(3000.0, FrequencyBand.HighMid)]
    [InlineData(6000.0, FrequencyBand.Presence)]
    [InlineData(12000.0, FrequencyBand.Air)]
    public void Extract_ToneInEachBand_MakesThatBandDominant(double frequencyHz, FrequencyBand expected)
    {
        SpectralFacet facet = Analyze(AudioSynth.Sine(frequencyHz, 1.0));

        Assert.Equal(expected, facet.Bands.Dominant);
    }

    [Fact]
    public void Extract_BandEnergies_SumToOne()
    {
        SpectralFacet facet = Analyze(AudioSynth.Noise(1.0));

        Assert.Equal(1.0, facet.Bands.ToArray().Sum(), 0.01);
    }

    [Fact]
    public void Extract_WhiteNoise_IsFlatAndSpreadInProportionToBandwidth()
    {
        SpectralFacet facet = Analyze(AudioSynth.Noise(2.0));

        Assert.True(facet.Flatness > 0.4, $"flatness {facet.Flatness}");
        Assert.Equal(0.64, facet.Bands.Air, 0.06);
        Assert.True(facet.Bands.Sub < 0.02);
    }

    [Fact]
    public void Extract_TwoTones_SplitEnergyBetweenTheirBands()
    {
        float[] low = AudioSynth.Sine(150, 1.0, amplitude: 0.4);
        float[] high = AudioSynth.Sine(6000, 1.0, amplitude: 0.4);
        float[] both = [.. low.Zip(high, (a, b) => a + b)];

        SpectralFacet facet = Analyze(both);

        Assert.Equal(0.5, facet.Bands.Bass, 0.05);
        Assert.Equal(0.5, facet.Bands.Presence, 0.05);
        Assert.True(facet.RolloffHz > 4000);
    }

    [Fact]
    public void Extract_SameToneAt96Khz_GivesTheSameCentroid()
    {
        SpectralFacet at44 = Analyze(AudioSynth.Sine(3000, 1.0));
        SpectralFacet at96 = Analyze(AudioSynth.Sine(3000, 1.0, sampleRate: 96000), 96000);

        Assert.Equal(at44.CentroidHz, at96.CentroidHz, 100.0);
        Assert.Equal(FrequencyBand.HighMid, at96.Bands.Dominant);
    }

    [Fact]
    public void Extract_ShortBurstFollowedByLongSilence_IsNotDilutedByTheSilence()
    {
        float[] burst = AudioSynth.Decay(AudioSynth.Noise(0.1, amplitude: 0.8), 80);

        SpectralFacet alone = Analyze(burst);
        SpectralFacet padded = Analyze(AudioSynth.Concat(burst, AudioSynth.Silence(5.0)));

        Assert.Equal(alone.CentroidHz, padded.CentroidHz, alone.CentroidHz * 0.1);
    }

    [Fact]
    public void Extract_BrighterSignal_HasAHigherCentroidAndRolloff()
    {
        SpectralFacet dark = Analyze(AudioSynth.Sine(300, 1.0));
        SpectralFacet bright = Analyze(AudioSynth.Sine(5000, 1.0));

        Assert.True(bright.CentroidHz > dark.CentroidHz * 5);
        Assert.True(bright.RolloffHz > dark.RolloffHz);
    }

    [Fact]
    public void Extract_NaturalLowPassedSpectrum_HasANegativeTilt()
    {
        float[] noise = AudioSynth.Noise(2.0);
        float[] darkNoise = Biquad.LowPass(AudioSynth.Rate, 1000).Process(Biquad.LowPass(AudioSynth.Rate, 1000).Process(noise));

        SpectralFacet white = Analyze(noise);
        SpectralFacet dark = Analyze(darkNoise);

        Assert.True(dark.TiltDbPerOctave < white.TiltDbPerOctave - 6.0, $"white {white.TiltDbPerOctave}, dark {dark.TiltDbPerOctave}");
    }

    [Fact]
    public void Extract_Silence_ReturnsZeroedFacet()
    {
        SpectralFacet facet = Analyze(AudioSynth.Silence(0.5));

        Assert.Equal(0.0, facet.CentroidHz);
        Assert.Equal(0.0, facet.Bands.ToArray().Sum());
    }
}
