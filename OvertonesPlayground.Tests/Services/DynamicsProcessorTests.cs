using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

public sealed class DynamicsProcessorTests
{
    #region Constants
    private const int SampleRate = 44100;
    #endregion

    #region Private methods
    private static short[] Constant(short value, int frames, int channels = 1) => Enumerable.Repeat(value, frames * channels).ToArray();
    #endregion

    #region Public methods
    [Fact]
    public void Compress_EmptyBuffer_DoesNothing() { DynamicsProcessor.Compress([], channels: 1, SampleRate, thresholdDb: -20, ratio: 4, attackMs: 5, releaseMs: 50); }
    [Fact]
    public void Compress_HigherRatio_ReducesTheSignalMore()
    {
        short[] gentle = Constant(short.MaxValue / 2, SampleRate / 4);
        short[] hard = (short[])gentle.Clone();

        DynamicsProcessor.Compress(gentle, channels: 1, SampleRate, thresholdDb: -20, ratio: 2, attackMs: 1, releaseMs: 1);
        DynamicsProcessor.Compress(hard, channels: 1, SampleRate, thresholdDb: -20, ratio: 10, attackMs: 1, releaseMs: 1);

        Assert.True(hard[^1] < gentle[^1], $"ratio 10 left {hard[^1]}, ratio 2 left {gentle[^1]}");
        Assert.True(gentle[^1] < short.MaxValue / 2);
    }

    [Fact]
    public void Compress_NeverExceedsThe16BitRange()
    {
        short[] samples = Constant(short.MinValue, 1000);

        DynamicsProcessor.Compress(samples, channels: 1, SampleRate, thresholdDb: -6, ratio: 2, attackMs: 1, releaseMs: 1);

        Assert.All(samples, sample => Assert.InRange(sample, short.MinValue, short.MaxValue));
    }

    [Fact]
    public void Compress_RatioOfOne_IsTransparentEvenAboveThreshold()
    {
        // The gain works out to 0.99999... in floating point; rounding (not truncating) keeps every sample exactly as it was.
        short[] samples = Constant(short.MaxValue / 2, SampleRate / 4);
        short[] original = (short[])samples.Clone();

        DynamicsProcessor.Compress(samples, channels: 1, SampleRate, thresholdDb: -20, ratio: 1, attackMs: 1, releaseMs: 1);

        Assert.Equal(original, samples);
    }

    [Fact]
    public void Compress_SignalBelowThreshold_IsLeftUntouched()
    {
        short[] samples = TestSignals.Sine(440, SampleRate, 0.25, amplitude: 0.05); // about -26 dBFS peak
        short[] original = (short[])samples.Clone();

        DynamicsProcessor.Compress(samples, channels: 1, SampleRate, thresholdDb: -12, ratio: 4, attackMs: 5, releaseMs: 50);

        Assert.Equal(original, samples);
    }

    [Fact]
    public void Compress_SlowAttack_LeavesTheOpeningOfALoudSignalUncompressedUntilTheEnvelopeRises()
    {
        // With a 100 ms attack the envelope only reaches the -20 dB threshold about 22 ms in; a fast attack would
        // already be compressing at 5 ms. The release is deliberately fast so a swapped attack/release would show.
        short[] samples = Constant(short.MaxValue / 2, SampleRate / 4);
        short original = samples[0];
        int fiveMillisecondsIn = SampleRate * 5 / 1000;

        DynamicsProcessor.Compress(samples, channels: 1, SampleRate, thresholdDb: -20, ratio: 8, attackMs: 100, releaseMs: 1);

        Assert.Equal(original, samples[fiveMillisecondsIn]);
        Assert.True(samples[^1] < original / 2, "the compressor should have clamped down by the end");
    }

    [Fact]
    public void Compress_SlowRelease_KeepsReducingGainAfterTheSignalDrops()
    {
        // A loud burst followed by a quieter (but still above-threshold) passage. A slow release lets the envelope
        // linger near the burst's level, so the quieter passage stays more compressed than with a fast release.
        short[] Burst()
        {
            short[] samples = new short[SampleRate * 3 / 10];
            Array.Fill(samples, (short)(0.9 * short.MaxValue), 0, SampleRate / 4);
            Array.Fill(samples, (short)(0.3 * short.MaxValue), SampleRate / 4, samples.Length - (SampleRate / 4));
            return samples;
        }

        short[] slowRelease = Burst();
        short[] fastRelease = Burst();
        int tenMillisecondsAfterDrop = (SampleRate / 4) + (SampleRate / 100);

        DynamicsProcessor.Compress(slowRelease, channels: 1, SampleRate, thresholdDb: -20, ratio: 4, attackMs: 1, releaseMs: 500);
        DynamicsProcessor.Compress(fastRelease, channels: 1, SampleRate, thresholdDb: -20, ratio: 4, attackMs: 1, releaseMs: 1);

        Assert.True(slowRelease[tenMillisecondsAfterDrop] < fastRelease[tenMillisecondsAfterDrop], $"slow release left {slowRelease[tenMillisecondsAfterDrop]}, fast release left {fastRelease[tenMillisecondsAfterDrop]}");
    }

    [Fact]
    public void Compress_SteadySignalAboveThreshold_SettlesToTheStaticGainCurve()
    {
        // A steady 0.9 FS signal against a -20 dB threshold at 4:1: envelope -0.915 dB, so the output settles at
        // -20 + (19.085 / 4) = -15.23 dB relative to full scale, i.e. about 0.1737 of the input's 0.9.
        short[] samples = Constant((short)(0.9 * short.MaxValue), SampleRate / 2);

        DynamicsProcessor.Compress(samples, channels: 1, SampleRate, thresholdDb: -20, ratio: 4, attackMs: 1, releaseMs: 1);

        double settled = samples[^1] / (double)short.MaxValue;
        Assert.InRange(settled, 0.16, 0.19);
    }

    [Fact]
    public void Compress_Stereo_AppliesTheSameGainToBothChannels()
    {
        // Loud left, quiet right: the right channel is ducked by the left's envelope, preserving the stereo image.
        short[] samples = new short[SampleRate / 4 * 2];
        for (int frame = 0; frame < samples.Length / 2; frame++)
        {
            samples[frame * 2] = (short)(0.9 * short.MaxValue);
            samples[(frame * 2) + 1] = (short)(0.09 * short.MaxValue);
        }

        DynamicsProcessor.Compress(samples, channels: 2, SampleRate, thresholdDb: -20, ratio: 4, attackMs: 1, releaseMs: 1);

        double left = samples[^2];
        double right = samples[^1];
        Assert.Equal(0.1, right / left, 2); // the original 10:1 left/right level ratio survives
        Assert.True(left < 0.9 * short.MaxValue / 2);
    }

    [Fact]
    public void Compress_ZeroAttackAndRelease_AreFlooredInsteadOfProducingNaN()
    {
        short[] samples = Constant(short.MaxValue / 2, 2000);

        DynamicsProcessor.Compress(samples, channels: 1, SampleRate, thresholdDb: -20, ratio: 4, attackMs: 0, releaseMs: -5);

        Assert.All(samples, sample => Assert.InRange(sample, (short)1, short.MaxValue / 2));
    }

    [Fact]
    public void Compress_ZeroChannels_DoesNothing()
    {
        short[] samples = Constant(1000, 100);
        short[] original = (short[])samples.Clone();

        DynamicsProcessor.Compress(samples, channels: 0, SampleRate, thresholdDb: -20, ratio: 4, attackMs: 5, releaseMs: 50);

        Assert.Equal(original, samples);
    }
    #endregion
}
