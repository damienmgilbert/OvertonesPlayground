namespace OvertonesPlayground.Tests.Ontology;

public sealed class DynamicsAnalyzerTests
{
    private readonly DynamicsAnalyzer _analyzer = new();

    private DynamicsFacet Analyze(float[] signal) => _analyzer.Extract(AudioSynth.Mono(signal));

    #region Levels
    [Fact]
    public void Extract_SineAtHalfScale_ReportsPeakRmsAndCrestFactor()
    {
        DynamicsFacet facet = Analyze(AudioSynth.Sine(1000, 1.0, amplitude: 0.5));

        Assert.Equal(-6.02, facet.PeakDb, 0.1);
        Assert.Equal(-9.03, facet.ActiveRmsDb, 0.1);
        Assert.Equal(3.01, facet.CrestDb, 0.1);
    }

    [Fact]
    public void Extract_SineWhoseSamplesMissTheCrest_ReportsATruePeakAboveTheSamplePeak()
    {
        float[] signal = AudioSynth.Sine(AudioSynth.Rate / 4.0, 0.5, amplitude: 1.0, phase: Math.PI / 4);

        DynamicsFacet facet = Analyze(signal);

        Assert.Equal(-3.01, facet.PeakDb, 0.1);
        Assert.True(facet.TruePeakDb > facet.PeakDb + 2.0, $"true peak {facet.TruePeakDb} should exceed sample peak {facet.PeakDb}");
    }

    [Fact]
    public void Extract_ConstantOffset_ReportsDcOffset()
    {
        float[] signal = [.. AudioSynth.Sine(500, 1.0, amplitude: 0.3).Select(sample => sample + 0.1f)];

        DynamicsFacet facet = Analyze(signal);

        Assert.Equal(0.1, facet.DcOffset, 0.005);
    }

    [Fact]
    public void Extract_DigitalSilence_ReturnsTheFloorWithoutThrowing()
    {
        DynamicsFacet facet = Analyze(AudioSynth.Silence(0.5));

        Assert.Equal(Decibels.Floor, facet.PeakDb);
        Assert.Equal(Decibels.Floor, facet.IntegratedLufs);
        Assert.Equal(0, facet.ClippedSamples);
    }
    #endregion

    #region Loudness (ITU-R BS.1770-4)
    [Fact]
    public void Extract_StereoSineAtMinus23Dbfs_MeasuresMinus23Lufs()
    {
        float[] channel = AudioSynth.Sine(1000, 5.0, amplitude: Math.Pow(10, -23.0 / 20.0));
        AnalysisContext context = AudioSynth.Context(AudioSynth.Rate, null, channel, channel);

        DynamicsFacet facet = _analyzer.Extract(context);

        Assert.True(facet.LufsIsGated);
        Assert.Equal(-23.0, facet.IntegratedLufs, 0.2);
    }

    [Fact]
    public void Extract_MonoSineIsThreeDecibelsQuieterThanTheSameSineInBothChannels()
    {
        float[] channel = AudioSynth.Sine(1000, 5.0, amplitude: 0.1);

        DynamicsFacet mono = Analyze(channel);
        DynamicsFacet stereo = _analyzer.Extract(AudioSynth.Context(AudioSynth.Rate, null, channel, channel));

        Assert.Equal(3.01, stereo.IntegratedLufs - mono.IntegratedLufs, 0.05);
    }

    [Fact]
    public void Extract_ShortSound_IsMeasuredUngatedOverItsAudiblePartOnly()
    {
        float[] tone = AudioSynth.Sine(1000, 0.4, amplitude: Math.Pow(10, -26.0 / 20.0));
        float[] padded = AudioSynth.Concat(tone, AudioSynth.Silence(2.0));
        float[] bare = tone;

        DynamicsFacet withSilence = Analyze(padded);
        DynamicsFacet withoutSilence = Analyze(bare);

        Assert.False(withSilence.LufsIsGated);
        Assert.Equal(withoutSilence.IntegratedLufs, withSilence.IntegratedLufs, 0.3);
    }

    [Fact]
    public void Extract_QuietPassageAmongLoudOnes_IsGatedOutOfIntegratedLoudness()
    {
        float[] loud = AudioSynth.Sine(1000, 4.0, amplitude: 0.3);
        float[] quiet = AudioSynth.Sine(1000, 4.0, amplitude: 0.003);
        float[] alone = loud;

        DynamicsFacet mixed = Analyze(AudioSynth.Concat(loud, quiet, loud));
        DynamicsFacet reference = Analyze(alone);

        Assert.Equal(reference.IntegratedLufs, mixed.IntegratedLufs, 0.3);
    }

    [Fact]
    public void Extract_SteadyToneLongerThanThreeSeconds_HasNearZeroLoudnessRange()
    {
        DynamicsFacet facet = Analyze(AudioSynth.Sine(1000, 8.0, amplitude: 0.2));

        Assert.True(facet.LoudnessRangeLu < 0.5);
    }
    #endregion

    #region Clipping
    [Fact]
    public void Extract_HardClippedSine_CountsTheFlatTops()
    {
        float[] clipped = [.. AudioSynth.Sine(100, 1.0, amplitude: 2.0).Select(sample => Math.Clamp(sample, -1f, 1f))];

        DynamicsFacet facet = Analyze(clipped);

        Assert.True(facet.ClippedSamples > 100, $"clipped samples: {facet.ClippedSamples}");
    }

    [Fact]
    public void Extract_FullScaleLowFrequencySine_IsNotMistakenForClipping()
    {
        DynamicsFacet facet = Analyze(AudioSynth.Sine(40, 1.0, amplitude: 1.0));

        Assert.Equal(0, facet.ClippedSamples);
    }
    #endregion

    #region Silence
    [Fact]
    public void Extract_SilenceAroundATone_ReportsLeadAndTrailInMilliseconds()
    {
        float[] signal = AudioSynth.Concat(AudioSynth.Silence(0.1), AudioSynth.Sine(1000, 0.5, amplitude: 0.5), AudioSynth.Silence(0.2));

        DynamicsFacet facet = Analyze(signal);

        Assert.Equal(100.0, facet.LeadSilenceMs, 5.0);
        Assert.Equal(200.0, facet.TrailSilenceMs, 5.0);
        Assert.Equal(0.5, facet.ActiveSeconds, 0.05);
    }
    #endregion

    #region Envelope
    [Fact]
    public void Extract_ShortNoiseBurst_HasAFastAttackAndAnImpulsiveEnvelope()
    {
        float[] burst = AudioSynth.Decay(AudioSynth.Noise(0.4, amplitude: 0.8), 200);

        DynamicsFacet facet = Analyze(burst);

        Assert.True(facet.AttackMs <= 5, $"attack {facet.AttackMs} ms");
        Assert.True(facet.DecayMs < 200, $"decay {facet.DecayMs} ms");
        Assert.Equal(EnvelopeShape.Impulsive, facet.Shape);
    }

    [Fact]
    public void Extract_ToneRingingOutOverSeveralSeconds_IsPlucked()
    {
        float[] ring = AudioSynth.Decay(AudioSynth.Sine(440, 3.0, amplitude: 0.8), 20);

        DynamicsFacet facet = Analyze(ring);

        Assert.Equal(EnvelopeShape.Plucked, facet.Shape);
        Assert.True(facet.DecayMs > 800, $"decay {facet.DecayMs} ms");
    }

    [Fact]
    public void Extract_HeldToneWithAFadeOut_IsSustained()
    {
        float[] held = AudioSynth.Shape(AudioSynth.Sine(440, 2.0, amplitude: 0.5), t => t < 1.6 ? 1.0 : Math.Max(0.0, 1.0 - ((t - 1.6) / 0.4)));

        DynamicsFacet facet = Analyze(held);

        Assert.Equal(EnvelopeShape.Sustained, facet.Shape);
    }

    [Fact]
    public void Extract_HeldToneCutOffAbruptly_IsGated()
    {
        float[] gated = AudioSynth.Shape(AudioSynth.Sine(440, 1.0, amplitude: 0.5), t => t < 0.8 ? 1.0 : 0.0);

        DynamicsFacet facet = Analyze(gated);

        Assert.Equal(EnvelopeShape.Gated, facet.Shape);
    }

    [Fact]
    public void Extract_RampingUpToTheVeryEnd_IsReverse()
    {
        float[] reversed = AudioSynth.Shape(AudioSynth.Noise(2.0, amplitude: 0.8), t => t / 2.0);

        DynamicsFacet facet = Analyze(reversed);

        Assert.Equal(EnvelopeShape.Reverse, facet.Shape);
    }

    [Fact]
    public void Extract_RisingThenFalling_IsASwell()
    {
        float[] swell = AudioSynth.Shape(AudioSynth.Noise(2.0, amplitude: 0.8), t => t < 1.0 ? t : 2.0 - t);

        DynamicsFacet facet = Analyze(swell);

        Assert.Equal(EnvelopeShape.Swell, facet.Shape);
        Assert.True(facet.AttackMs > 400);
    }
    #endregion
}
