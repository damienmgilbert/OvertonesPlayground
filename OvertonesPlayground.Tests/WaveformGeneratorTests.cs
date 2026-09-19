using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests;

public sealed class WaveformGeneratorTests
{
    private const int SampleRate = 8000;

    [Theory]
    [InlineData(1.0, 8000)]
    [InlineData(0.5, 4000)]
    [InlineData(0.0, 1)]
    [InlineData(-3.0, 1)]
    public void Generate_ProducesDurationTimesSampleRateFrames_WithAtLeastOne(double seconds, int expectedFrames)
    {
        short[] samples = WaveformGenerator.Generate(WaveformType.Sine, 440, seconds, SampleRate, amplitude: 0.5);

        Assert.Equal(expectedFrames, samples.Length);
    }

    [Fact]
    public void Generate_Sine_ReachesAmplitudeAtAQuarterCycleAndCrossesZeroAtAHalf()
    {
        // 1 kHz at 8 kHz: 8 samples per cycle, so index 2 is the quarter-cycle peak and index 4 the half-cycle zero.
        short[] samples = WaveformGenerator.Generate(WaveformType.Sine, 1000, 0.1, SampleRate, amplitude: 0.5, attackSeconds: 0, releaseSeconds: 0);

        Assert.Equal(0, samples[0]);
        Assert.Equal(WaveformGenerator.ToShort(0.5), samples[2]);
        Assert.InRange(samples[4], -1, 1);
        Assert.Equal(WaveformGenerator.ToShort(-0.5), samples[6]);
    }

    [Fact]
    public void Generate_Square_OnlyEmitsPositiveAndNegativeAmplitude()
    {
        short[] samples = WaveformGenerator.Generate(WaveformType.Square, 1000, 0.1, SampleRate, amplitude: 0.5, attackSeconds: 0, releaseSeconds: 0);
        short high = WaveformGenerator.ToShort(0.5);
        short low = WaveformGenerator.ToShort(-0.5);

        Assert.All(samples, sample => Assert.True(sample == high || sample == low, $"unexpected level {sample}"));
        Assert.Contains(high, samples);
        Assert.Contains(low, samples);
    }

    [Fact]
    public void Generate_Sawtooth_RampsUpFromMinusAmplitudeAndWrapsAtTheNextCycle()
    {
        // 100 Hz at 8 kHz: one cycle is 80 samples.
        short[] samples = WaveformGenerator.Generate(WaveformType.Sawtooth, 100, 0.05, SampleRate, amplitude: 1.0, attackSeconds: 0, releaseSeconds: 0);

        Assert.Equal(-short.MaxValue, samples[0]);
        for (int i = 1; i < 80; i++)
        {
            Assert.True(samples[i] > samples[i - 1], $"sample {i} should rise");
        }

        Assert.Equal(-short.MaxValue, samples[80]); // wraps at the next cycle
    }

    [Fact]
    public void Generate_Triangle_RisesToThePeakAtMidCycleAndFallsBack()
    {
        short[] samples = WaveformGenerator.Generate(WaveformType.Triangle, 100, 0.05, SampleRate, amplitude: 1.0, attackSeconds: 0, releaseSeconds: 0);

        Assert.Equal(short.MaxValue, samples[0]);
        Assert.Equal(-short.MaxValue, samples[40]);
        Assert.True(samples[20] is > -100 and < 100, "the crossing should sit near zero");
    }

    [Theory]
    [InlineData(WaveformType.Sine)]
    [InlineData(WaveformType.Square)]
    [InlineData(WaveformType.Triangle)]
    [InlineData(WaveformType.Sawtooth)]
    [InlineData(WaveformType.WhiteNoise)]
    [InlineData(WaveformType.PinkNoise)]
    public void Generate_NeverExceedsTheRequestedAmplitude(WaveformType type)
    {
        const double amplitude = 0.4;
        short[] samples = WaveformGenerator.Generate(type, 220, 0.5, SampleRate, amplitude);
        double limit = amplitude * short.MaxValue;

        Assert.All(samples, sample => Assert.InRange(Math.Abs((double)sample), 0, limit + 1));
    }

    [Theory]
    [InlineData(WaveformType.WhiteNoise)]
    [InlineData(WaveformType.PinkNoise)]
    public void Generate_Noise_ActuallyVaries(WaveformType type)
    {
        short[] samples = WaveformGenerator.Generate(type, 0, 0.5, SampleRate, amplitude: 0.8, attackSeconds: 0, releaseSeconds: 0);

        Assert.True(samples.Distinct().Count() > 100, "noise should not collapse to a handful of values");
    }

    [Fact]
    public void Generate_UnknownWaveformType_IsSilent()
    {
        short[] samples = WaveformGenerator.Generate((WaveformType)999, 440, 0.1, SampleRate, amplitude: 1.0);

        Assert.All(samples, sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void Generate_Envelope_FadesInFromSilenceAndOutToNearSilence()
    {
        short[] samples = WaveformGenerator.Generate(WaveformType.Square, 100, 1.0, SampleRate, amplitude: 1.0, attackSeconds: 0.1, releaseSeconds: 0.1);

        Assert.Equal(0, samples[0]);
        Assert.True(Math.Abs((int)samples[^1]) < 100, "the final sample should be nearly silent");
    }

    [Fact]
    public void Generate_Envelope_LeavesTheMiddleOfTheToneAtFullAmplitude()
    {
        short[] samples = WaveformGenerator.Generate(WaveformType.Square, 100, 1.0, SampleRate, amplitude: 1.0, attackSeconds: 0.1, releaseSeconds: 0.1);

        Assert.All(samples[1000..7000], sample => Assert.True(Math.Abs((int)sample) >= short.MaxValue));
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(1.0, short.MaxValue)]
    [InlineData(-1.0, -short.MaxValue)]
    [InlineData(0.5, 16383)]
    [InlineData(2.0, short.MaxValue)]
    [InlineData(-2.0, short.MinValue)]
    public void ToShort_ScalesToFullRangeAndClampsOverdrive(double normalized, short expected)
    {
        Assert.Equal(expected, WaveformGenerator.ToShort(normalized));
    }
}
